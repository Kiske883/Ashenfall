using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Ashenfall.Api.Catalog.Domain;

namespace Ashenfall.Api.Catalog.Services;

/// <summary>Version 1 canonical binary catalog content; no timestamps or release UUID.</summary>
public static class CatalogHash
{
    public const string Format = "ASHENFALL-CATALOG-CONTENT-V1";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static string Compute(CatalogSnapshot snapshot)
    {
        using var stream = new MemoryStream();
        var writer = new CanonicalWriter(stream);
        writer.String(Format);
        writer.Int32(snapshot.Release.SchemaMajor);
        writer.String(snapshot.Release.VersionKey);
        var classes = snapshot.Classes.OrderBy(x => x.Code, StringComparer.Ordinal).ToArray();
        writer.Int32(classes.Length);
        foreach (var x in classes)
        {
            writer.Guid(x.Id); writer.String(x.Code); writer.String(x.DisplayName); writer.String(x.VisualKey);
            writer.Int32(x.BaseMaxHealth); writer.Int32(x.FixedDamage);
            writer.NullableInt32(x.BaseStrength); writer.NullableInt32(x.BaseDexterity);
            writer.NullableInt32(x.BaseVitality); writer.NullableInt32(x.BaseArmor);
            writer.Decimal3(x.MoveSpeedMps); writer.Decimal3(x.MeleeRangeM);
            writer.Int32(x.AttackCycleMs); writer.Int32(x.ImpactMs);
        }
        var enemies = snapshot.Enemies.OrderBy(x => x.Code, StringComparer.Ordinal).ToArray();
        writer.Int32(enemies.Length);
        foreach (var x in enemies)
        {
            writer.Guid(x.Id); writer.String(x.Code); writer.String(x.DisplayName); writer.String(x.VisualKey);
            writer.Int32(x.MaxHealth); writer.Int32(x.FixedDamage);
            writer.NullableInt32(x.BaseStrength); writer.NullableInt32(x.BaseDexterity);
            writer.NullableInt32(x.BaseVitality); writer.NullableInt32(x.BaseArmor);
            writer.Decimal3(x.AttackRangeM); writer.Int32(x.WindupMs);
            writer.Int32(x.RecoveryMs); writer.Int32(x.ImpactAfterExecuteMs);
            writer.Decimal3(x.ChaseSpeedMps); writer.Decimal3(x.ReturnSpeedMps);
            writer.Decimal3(x.DetectionRadiusM); writer.Decimal3(x.DisengagementRadiusM);
            writer.Decimal3(x.MaxSpawnDistanceM); writer.Decimal3(x.StoppingDistanceM);
            writer.Decimal3(x.ResumeDistanceM);
        }
        var items = snapshot.Items.OrderBy(x => x.Code, StringComparer.Ordinal).ToArray();
        writer.Int32(items.Length);
        foreach (var x in items)
        {
            writer.Guid(x.Id); writer.String(x.Code); writer.String(x.DisplayName);
            writer.String(x.Category); writer.String(x.Rarity); writer.Bool(x.Stackable);
            writer.Int32(x.MaxStack); writer.String(x.EquipSlot);
            writer.NullableInt32(x.HandRequirement); writer.String(x.VisualKey);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private sealed class CanonicalWriter(Stream output)
    {
        public void Int32(int value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            output.Write(bytes);
        }

        public void Bool(bool value) => output.WriteByte(value ? (byte)1 : (byte)0);
        public void Guid(Guid value) => String(value.ToString("D"));
        public void NullableInt32(int? value) { Bool(value.HasValue); if (value.HasValue) Int32(value.Value); }
        public void String(string? value)
        {
            if (value is null) { Int32(-1); return; }
            byte[] bytes = Utf8.GetBytes(value.Normalize(NormalizationForm.FormC));
            Int32(bytes.Length);
            output.Write(bytes);
        }
        public void Decimal3(decimal value)
        {
            decimal scaled = value * 1000m;
            if (decimal.Truncate(scaled) != scaled || scaled < int.MinValue || scaled > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), "Catalog decimal must have at most three fractional places.");
            Int32((int)scaled);
        }
    }
}
