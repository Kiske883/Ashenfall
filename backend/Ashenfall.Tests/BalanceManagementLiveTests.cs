using System.Data;
using Ashenfall.CatalogPublisher.Balance;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace Ashenfall.Tests;

public sealed class BalanceWriteFactAttribute : FactAttribute
{
    public BalanceWriteFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ASHENFALL_09C4_WRITE_TESTS") != "1")
            Skip = "Needs separate owner authorization for rolled-back writes on ashenfall_09b3_test; no fallback database.";
    }
}

/// <summary>Real PostgreSQL tests, opt-in only. Every outer transaction rolls back, including on assertion failure.</summary>
[Collection("V2Publication")]
public sealed class BalanceManagementLiveTests(ITestOutputHelper output)
{
    private static async Task<NpgsqlConnection> Open()
    {
        var c = new NpgsqlConnection(BalanceManagementService.LocalConnection); await c.OpenAsync(); return c;
    }
    private static BalancePlan Change(BalanceState state,decimal chance) => BalancePlanner.Plan(state,Guid.NewGuid(),
        "balance-live-"+Guid.NewGuid().ToString("N"),[new BalanceEdit("class/dwarf_warrior/critical_chance_percent",chance)],
        characterId:state.Characters.Single(x=>x.Id==Guid.Parse("d9a62a3c-4996-4276-8ab8-2c4d6f2ec988")).Id);

    [BalanceWriteFact]
    public async Task RealApplyRestoreDuplicateAndNoOpPreserveCharacterAndRollbackAllCatalogWrites()
    {
        var service=new BalanceManagementService();var before=await service.InspectAsync();
        Assert.Equal(5,BalancePlanner.Snapshot(before,before.Active,before.Characters.Single(x=>x.Id==Guid.Parse("d9a62a3c-4996-4276-8ab8-2c4d6f2ec988")).Id).EffectiveStats.CriticalChancePercent);
        await using(var c=await Open())
        await using(var tx=await c.BeginTransactionAsync(IsolationLevel.ReadCommitted))
        {
            try
            {
                var plan=Change(before,100);
                var applied=await service.ApplyInTransactionAsync(c,tx,plan);
                Assert.Equal("Published",applied.Result);Assert.Equal(100,applied.Snapshot!.EffectiveStats.CriticalChancePercent);
                Assert.Equal(105,applied.Snapshot.EffectiveStats.MaxHealth);Assert.Equal(27,applied.Snapshot.EffectiveStats.MinDamage);
                Assert.Equal(27,applied.Snapshot.EffectiveStats.MaxDamage);Assert.Equal(100,applied.Snapshot.EffectiveStats.CriticalDamageBonusPercent);
                Assert.Equal(1,applied.Snapshot.CharacterRevision);
                output.WriteLine($"Transactional publication: {applied.ReleaseId}; V2={applied.Hash}; HP=105; damage=27-27; chance=100; bonus=100; revision=1");
                var duplicate=await service.ApplyInTransactionAsync(c,tx,plan);Assert.StartsWith("AlreadyApplied",duplicate.Result);
                output.WriteLine("Duplicate operation rejected as a replay: AlreadyApplied.");
                var current=await BalanceManagementService.LoadAsync(c,tx);
                var noop=Change(current,100);Assert.True(noop.NoOp);
                Assert.Equal("NoOp",(await service.ApplyInTransactionAsync(c,tx,noop)).Result);
                Assert.Equal(current.Catalogs.Count,(await BalanceManagementService.LoadAsync(c,tx)).Catalogs.Count);
                var restore=BalancePlanner.Plan(current,Guid.NewGuid(),"restore-live-"+Guid.NewGuid().ToString("N"),[],
                    before.Active.Catalog.Release.Id,plan.CharacterId);
                var restored=await service.ApplyInTransactionAsync(c,tx,restore);
                Assert.Equal(5,restored.Snapshot!.EffectiveStats.CriticalChancePercent);
                Assert.Equal(105,restored.Snapshot.EffectiveStats.MaxHealth);Assert.Equal(27,restored.Snapshot.EffectiveStats.MinDamage);
                Assert.Equal(27,restored.Snapshot.EffectiveStats.MaxDamage);Assert.Equal(100,restored.Snapshot.EffectiveStats.CriticalDamageBonusPercent);
                Assert.Equal(1,restored.Snapshot.CharacterRevision);
                Assert.NotEqual(before.Active.Catalog.Release.Id,restored.ReleaseId);
                var state=await BalanceManagementService.LoadAsync(c,tx);
                state.Active.Verify();Assert.Single(state.Catalogs,x=>x.Catalog.Release.Status=="published");
                Assert.Equal("retired",state.Catalogs.Single(x=>x.Catalog.Release.Id==before.Active.Catalog.Release.Id).Catalog.Release.Status);
                output.WriteLine($"Forward restoration: {restored.ReleaseId}; V2={restored.Hash}; chance=5; other effective stats unchanged; exactly one active release.");
                Assert.Equal(before.Active.Hash,state.Catalogs.Single(x=>x.Catalog.Release.Id==before.Active.Catalog.Release.Id).Hash);
                Assert.Equal(100,state.Characters.Single(x=>x.Id==plan.CharacterId).Experience);
                await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ApplyInTransactionAsync(c,tx,plan with { TargetHash=new string('0',64) }));
                Assert.Equal(restored.ReleaseId,(await BalanceManagementService.LoadAsync(c,tx)).Active.Catalog.Release.Id);
                output.WriteLine("Hash rejection preserved the active restoration; NoOp inserted no release; outer transaction will roll back.");
            }
            finally { await tx.RollbackAsync(); }
        }
        var after=await service.InspectAsync();Assert.Equal(before.Active.Hash,after.Active.Hash);
        Assert.Equal(before.Catalogs.Count,after.Catalogs.Count);
    }

    [BalanceWriteFact]
    public async Task RealConcurrentAttemptsWaitForPublicationLockAndRejectedPlanLeavesNoDraft()
    {
        var service=new BalanceManagementService();var before=await service.InspectAsync();var plan=Change(before,100);
        await using var first=await Open();await using var second=await Open();
        await using var tx1=await first.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await using var tx2=await second.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        bool firstRolledBack=false;
        try
        {
            await service.ApplyInTransactionAsync(first,tx1,plan,deadline.Token);
            var attempt=service.ApplyInTransactionAsync(second,tx2,plan,deadline.Token);
            bool blocked=false;
            for(int poll=0;poll<50 && !attempt.IsCompleted;poll++)
            {
                await using var waiting=new NpgsqlCommand("""
                    SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE pid=@waiter
                      AND wait_event_type='Lock' AND wait_event='advisory'
                      AND @holder=ANY(pg_blocking_pids(pid)))
                    """,first,tx1);
                waiting.Parameters.AddWithValue("waiter",second.ProcessID);
                waiting.Parameters.AddWithValue("holder",first.ProcessID);
                if(await waiting.ExecuteScalarAsync(deadline.Token) is true) { blocked=true;break; }
                await Task.Delay(20,deadline.Token);
            }
            Assert.True(blocked,"PostgreSQL must report the second connection blocked on the first publisher's advisory lock.");
            Assert.False(attempt.IsCompleted);
            output.WriteLine($"PostgreSQL observed waiter PID {second.ProcessID} blocked on holder PID {first.ProcessID}: Lock/advisory and pg_blocking_pids match.");
            // This releases the real lock and restores the previous active release for the waiter.
            await tx1.RollbackAsync();firstRolledBack=true;
            var result=await attempt;Assert.Equal("Published",result.Result);
            output.WriteLine("Waiter publication completed only after holder rollback; secondary transaction is rollback-only.");
            var current=await BalanceManagementService.LoadAsync(second,tx2);
            var stale=Change(before,8);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ApplyInTransactionAsync(second,tx2,stale));
            Assert.DoesNotContain((await BalanceManagementService.LoadAsync(second,tx2)).Catalogs,x=>x.Catalog.Release.Id==stale.OperationId);
            Assert.Equal(result.ReleaseId,current.Active.Catalog.Release.Id);
            output.WriteLine("Stale plan rejected with no candidate draft remaining; secondary finally rolls back.");
        }
        finally
        {
            // Npgsql can retain Connection after completion; it does not mean the transaction is active.
            // Always reach the secondary rollback even if cleanup of the first transaction fails.
            try { if(!firstRolledBack) await tx1.RollbackAsync(); }
            finally { await tx2.RollbackAsync(); }
        }
        Assert.Equal(before.Active.Hash,(await service.InspectAsync()).Active.Hash);
    }
}
