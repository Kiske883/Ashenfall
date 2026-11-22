#nullable enable
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ashenfall.Catalog;

/// <summary>Independent Godot-side implementation of the approved canonical V1 binary contract.</summary>
public static class CatalogContentHash
{
    public const string Format = "ASHENFALL-CATALOG-CONTENT-V1";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static string Compute(CatalogSnapshot snapshot)
    {
        using var stream = new MemoryStream();
        var w = new Writer(stream);
        w.String(Format);
        w.Int32(snapshot.SchemaMajor);
        w.String(snapshot.VersionKey);
        var classes = snapshot.Classes.OrderBy(x => x.Code, StringComparer.Ordinal).ToArray();
        w.Int32(classes.Length);
        foreach (var x in classes)
        {
            w.Guid(x.Id); w.String(x.Code); w.String(x.DisplayName); w.String(x.VisualKey);
            w.Int32(x.BaseMaxHealth); w.Int32(x.FixedDamage);
            w.NullableInt32(x.BaseStrength); w.NullableInt32(x.BaseDexterity);
            w.NullableInt32(x.BaseVitality); w.NullableInt32(x.BaseArmor);
            w.Decimal3(x.MoveSpeedMps); w.Decimal3(x.MeleeRangeM);
            w.Int32(x.AttackCycleMs); w.Int32(x.ImpactMs);
        }
        var enemies = snapshot.Enemies.OrderBy(x => x.Code, StringComparer.Ordinal).ToArray();
        w.Int32(enemies.Length);
        foreach (var x in enemies)
        {
            w.Guid(x.Id); w.String(x.Code); w.String(x.DisplayName); w.String(x.VisualKey);
            w.Int32(x.MaxHealth); w.Int32(x.FixedDamage);
            w.NullableInt32(x.BaseStrength); w.NullableInt32(x.BaseDexterity);
            w.NullableInt32(x.BaseVitality); w.NullableInt32(x.BaseArmor);
            w.Decimal3(x.AttackRangeM); w.Int32(x.WindupMs);
            w.Int32(x.RecoveryMs); w.Int32(x.ImpactAfterExecuteMs);
            w.Decimal3(x.ChaseSpeedMps); w.Decimal3(x.ReturnSpeedMps);
            w.Decimal3(x.DetectionRadiusM); w.Decimal3(x.DisengagementRadiusM);
            w.Decimal3(x.MaxSpawnDistanceM); w.Decimal3(x.StoppingDistanceM);
            w.Decimal3(x.ResumeDistanceM);
        }
        var items = snapshot.Items.OrderBy(x => x.Code, StringComparer.Ordinal).ToArray();
        w.Int32(items.Length);
        foreach (var x in items)
        {
            w.Guid(x.Id); w.String(x.Code); w.String(x.DisplayName);
            w.String(x.Category); w.String(x.Rarity); w.Bool(x.Stackable);
            w.Int32(x.MaxStack); w.String(x.EquipSlot);
            w.NullableInt32(x.HandRequirement); w.String(x.VisualKey);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private sealed class Writer(Stream output)
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
            Int32(bytes.Length); output.Write(bytes);
        }
        public void Decimal3(decimal value)
        {
            decimal scaled = value * 1000m;
            if (decimal.Truncate(scaled) != scaled || scaled < int.MinValue || scaled > int.MaxValue)
                throw new CatalogLoadException("Catalog decimal precision is unsupported.");
            Int32((int)scaled);
        }
    }
}
