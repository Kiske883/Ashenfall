using System.Globalization;
using System.Text.Json;
using Ashenfall.CatalogPublisher.Balance;

// All business rules live in BalanceManagementService/BalancePlanner. This process never loads .env files or tokens.
try
{
    if (args.Length == 0 || args[0] is "help" or "--help")
    {
        Console.WriteLine("""
            Ashenfall Balance CLI (isolated database only)
            status
            parameters [--category class|enemy|level|modifier] [--filter substring] [--release GUID]
            inspect --character GUID
            history
            diff --from GUID --to GUID
            plan --version VERSION --set PARAMETER=VALUE [--set ...] [--character GUID] --out NEW.json
            restore-plan --from GUID --version VERSION [--character GUID] --out NEW.json
            validate --plan FILE.json
            apply --plan FILE.json [--apply]
            Values use invariant decimal notation: 5 means 5%, not 0.05. Use parameters for ranges and units.
            apply requires --apply for unattended execution, or interactive typed database confirmation.
            Credentials: existing private PostgreSQL pgpass. No password/token/connection arguments accepted.
            """);
        return 0;
    }
    var command = args[0];
    var options = new Dictionary<string,List<string>>(StringComparer.Ordinal);
    for (int i=1;i<args.Length;i++)
    {
        var key=args[i];
        if (key=="--apply") { if (!options.TryAdd(key, ["true"])) throw new ArgumentException("Duplicate flag."); continue; }
        if (!key.StartsWith("--",StringComparison.Ordinal) || i+1>=args.Length || args[i+1].StartsWith("--",StringComparison.Ordinal))
            throw new ArgumentException("Expected named option and value.");
        if (!options.TryGetValue(key,out var values)) options[key]=values=[];
        values.Add(args[++i]);
        if (key!="--set" && values.Count>1) throw new ArgumentException("Duplicate option.");
    }
    string[] allowed = command switch {
        "status" or "history" => [], "parameters" => ["--category","--filter","--release"],
        "inspect" => ["--character"], "diff" => ["--from","--to"],
        "plan" => ["--version","--set","--character","--out"],
        "restore-plan" => ["--from","--version","--character","--out"],
        "validate" => ["--plan"], "apply" => ["--plan","--apply"], _ => throw new ArgumentException("Unknown command; use help.") };
    if (options.Keys.Any(x=>!allowed.Contains(x))) throw new ArgumentException("Unsupported option; use help.");
    string Required(string key) => options.TryGetValue(key,out var v) ? v.Single() : throw new ArgumentException("Missing "+key);
    Guid Id(string key) => Guid.TryParse(Required(key),out var id) && id!=Guid.Empty ? id : throw new ArgumentException("Invalid GUID option.");
    Guid? OptionalId(string key) => options.ContainsKey(key) ? Id(key) : null;
    void Output(object value) => Console.WriteLine(JsonSerializer.Serialize(value,BalanceJson.Options));
    BalancePlan ReadPlan()
    {
        var file=new FileInfo(Required("--plan"));
        if (!file.Exists || file.Length>1024*1024) throw new ArgumentException("Plan missing or exceeds 1 MiB.");
        return JsonSerializer.Deserialize<BalancePlan>(File.ReadAllText(file.FullName),BalanceJson.Options) ?? throw new ArgumentException("Invalid plan JSON.");
    }
    var service=new BalanceManagementService();
    Console.Error.WriteLine("Target: "+BalanceManagementService.Target+" (no automatic target fallback)");
    if (command=="apply")
    {
        var plan=ReadPlan();
        if (!options.ContainsKey("--apply"))
        {
            if (Console.IsInputRedirected) throw new ArgumentException("Noninteractive publication requires --apply.");
            Console.Error.Write("Publish reviewed plan? Type ashenfall_09b3_test: ");
            if (Console.ReadLine()!="ashenfall_09b3_test") throw new ArgumentException("Publication cancelled.");
        }
        Output(await service.ApplyAsync(plan,true)); return 0;
    }
    var state=await service.InspectAsync(); state.Active.Verify();
    var active=state.Active;
    BalanceCatalog Release(Guid id) => state.Catalogs.Single(x=>x.Catalog.Release.Id==id);
    object Summary(BalanceCatalog c) => new { c.Catalog.Release.Id,c.Catalog.Release.VersionKey,c.Catalog.Release.Status,
        c.Catalog.Release.PublishedAt,V1Hash=c.Catalog.Release.ContentSha256,V2Hash=c.Hash,
        CatalogSchema=c.Catalog.Release.SchemaMajor,SnapshotSchema=string.IsNullOrEmpty(c.Hash)?1:2 };
    switch(command)
    {
        case "status": Output(new { Target=BalanceManagementService.Target,Active=Summary(active),Categories=new[]{"class","enemy","level","modifier"} }); break;
        case "history": Output(state.Catalogs.OrderBy(x=>x.Catalog.Release.CreatedAt).Select(Summary)); break;
        case "parameters":
            var selected=OptionalId("--release") is {} release ? Release(release) : active; selected.Verify();
            Output(BalanceParameters.Discover(selected)
                .Where(x=>!options.ContainsKey("--category") || x.Category==Required("--category"))
                .Where(x=>!options.ContainsKey("--filter") || x.Key.Contains(Required("--filter"),StringComparison.OrdinalIgnoreCase)).Select(x=>x.Describe())); break;
        case "inspect":
            var characterId=Id("--character");var character=state.Characters.Single(x=>x.Id==characterId && x.DeletedAt is null);
            Output(new { character.Id,character.ClassDefinitionId,character.Experience,character.RowRevision,
                Snapshot=BalancePlanner.Snapshot(state,active,characterId) }); break;
        case "diff":
            var from=Release(Id("--from")); var to=Release(Id("--to")); from.Verify();to.Verify();
            Output(new { From=Summary(from),To=Summary(to),AuthoredConfigurationChanged=BalancePlanner.ConfigurationSignature(from)!=BalancePlanner.ConfigurationSignature(to),
                Differences=BalancePlanner.Differences(from,to) }); break;
        case "validate":
            var reviewed=ReadPlan(); BalancePlanner.Materialize(state,reviewed); Output(new { Valid=true,Plan=reviewed }); break;
        case "plan": case "restore-plan":
            var edits=new List<BalanceEdit>();
            foreach(var text in options.GetValueOrDefault("--set",[]))
            {
                var split=text.Split('=');
                if(split.Length!=2 || !decimal.TryParse(split[1],NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var value))
                    throw new ArgumentException("Use PARAMETER=VALUE with decimal points; no percent sign or thousands separators.");
                edits.Add(new BalanceEdit(split[0],value));
            }
            if(command=="plan" && edits.Count==0) throw new ArgumentException("At least one --set is required.");
            var planned=BalancePlanner.Plan(state,Guid.NewGuid(),Required("--version"),edits,
                command=="restore-plan"?Id("--from"):null,OptionalId("--character"));
            // Never overwrite a previously reviewed plan or another file.
            using(var file=new FileStream(Required("--out"),FileMode.CreateNew,FileAccess.Write))
                JsonSerializer.Serialize(file,planned,BalanceJson.Options);
            Output(planned); break;
    }
    return 0;
}
catch(Exception error)
{
    // PostgreSQL/provider exceptions can contain server details: do not print arbitrary exception text.
    if(error is ArgumentException or InvalidOperationException && error.Source?.StartsWith("Npgsql",StringComparison.Ordinal)!=true)
        Console.Error.WriteLine("Rejected: "+error.Message);
    else Console.Error.WriteLine("Balance operation failed ("+error.GetType().Name+"). No partial publication was committed; inspect the target safely.");
    return 1;
}
