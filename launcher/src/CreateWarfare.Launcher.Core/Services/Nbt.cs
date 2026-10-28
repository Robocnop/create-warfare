using System.Buffers.Binary;
using System.Text;

namespace CreateWarfare.Launcher.Core.Services;

/// <summary>Une valeur NBT : son type (1 à 12) et sa valeur .NET.</summary>
public sealed record NbtTag(byte Type, object Value);

/// <summary>Compound NBT : garde l'ordre et les tags inconnus, pour réécrire le fichier sans rien perdre.</summary>
public sealed class NbtCompound
{
    public List<(string Name, NbtTag Tag)> Entries { get; } = new();

    public NbtTag? Get(string name) => Entries.FirstOrDefault(e => e.Name == name).Tag;
    public string? GetString(string name) => Get(name) is { Type: Nbt.String, Value: string s } ? s : null;
    public NbtCompound Set(string name, NbtTag tag)
    {
        var i = Entries.FindIndex(e => e.Name == name);
        if (i >= 0) Entries[i] = (name, tag); else Entries.Add((name, tag));
        return this;
    }
}

public sealed class NbtList
{
    public byte ElementType { get; set; }
    public List<object> Items { get; } = new();
}

/// <summary>NBT Java, non compressé et big-endian, comme servers.dat. Chaînes en UTF-8 modifié (Java).</summary>
public static class Nbt
{
    public const byte End = 0, Byte = 1, Short = 2, Int = 3, Long = 4, Float = 5, Double = 6,
        ByteArray = 7, String = 8, List = 9, Compound = 10, IntArray = 11, LongArray = 12;

    public static NbtCompound ReadFile(string path)
    {
        using var r = new BinaryReader(File.OpenRead(path));
        if (r.ReadByte() != Compound) throw new InvalidDataException($"{path} : la racine NBT n'est pas un compound");
        ReadString(r);
        return (NbtCompound)ReadPayload(r, Compound);
    }

    public static void WriteFile(string path, NbtCompound root)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(Compound);
            WriteString(w, "");
            WritePayload(w, Compound, root);
        }
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, ms.ToArray());
        File.Move(tmp, path, overwrite: true);
    }

    static object ReadPayload(BinaryReader r, byte type)
    {
        switch (type)
        {
            case Byte: return r.ReadSByte();
            case Short: return BinaryPrimitives.ReadInt16BigEndian(r.ReadBytes(2));
            case Int: return ReadInt(r);
            case Long: return BinaryPrimitives.ReadInt64BigEndian(r.ReadBytes(8));
            case Float: return BinaryPrimitives.ReadSingleBigEndian(r.ReadBytes(4));
            case Double: return BinaryPrimitives.ReadDoubleBigEndian(r.ReadBytes(8));
            case ByteArray: return r.ReadBytes(ReadInt(r));
            case String: return ReadString(r);
            case List:
            {
                var l = new NbtList { ElementType = r.ReadByte() };
                var n = ReadInt(r);
                for (int i = 0; i < n; i++) l.Items.Add(ReadPayload(r, l.ElementType));
                return l;
            }
            case Compound:
            {
                var c = new NbtCompound();
                byte t;
                while ((t = r.ReadByte()) != End)
                {
                    var name = ReadString(r);
                    c.Entries.Add((name, new NbtTag(t, ReadPayload(r, t))));
                }
                return c;
            }
            case IntArray: return Enumerable.Range(0, ReadInt(r)).Select(_ => ReadInt(r)).ToArray();
            case LongArray: return Enumerable.Range(0, ReadInt(r)).Select(_ => BinaryPrimitives.ReadInt64BigEndian(r.ReadBytes(8))).ToArray();
            default: throw new InvalidDataException($"Type NBT inconnu : {type}");
        }
    }

    static void WritePayload(BinaryWriter w, byte type, object v)
    {
        Span<byte> b = stackalloc byte[8];
        switch (type)
        {
            case Byte: w.Write((sbyte)v); break;
            case Short: BinaryPrimitives.WriteInt16BigEndian(b, (short)v); w.Write(b[..2]); break;
            case Int: WriteInt(w, (int)v); break;
            case Long: BinaryPrimitives.WriteInt64BigEndian(b, (long)v); w.Write(b[..8]); break;
            case Float: BinaryPrimitives.WriteSingleBigEndian(b, (float)v); w.Write(b[..4]); break;
            case Double: BinaryPrimitives.WriteDoubleBigEndian(b, (double)v); w.Write(b[..8]); break;
            case ByteArray: var bytes = (byte[])v; WriteInt(w, bytes.Length); w.Write(bytes); break;
            case String: WriteString(w, (string)v); break;
            case List:
                var l = (NbtList)v;
                w.Write(l.Items.Count == 0 ? End : l.ElementType);
                WriteInt(w, l.Items.Count);
                foreach (var item in l.Items) WritePayload(w, l.ElementType, item);
                break;
            case Compound:
                foreach (var (name, tag) in ((NbtCompound)v).Entries)
                {
                    w.Write(tag.Type);
                    WriteString(w, name);
                    WritePayload(w, tag.Type, tag.Value);
                }
                w.Write(End);
                break;
            case IntArray: var ia = (int[])v; WriteInt(w, ia.Length); foreach (var x in ia) WriteInt(w, x); break;
            case LongArray:
                var la = (long[])v; WriteInt(w, la.Length);
                foreach (var x in la) { BinaryPrimitives.WriteInt64BigEndian(b, x); w.Write(b[..8]); }
                break;
            default: throw new InvalidDataException($"Type NBT inconnu : {type}");
        }
    }

    static int ReadInt(BinaryReader r) => BinaryPrimitives.ReadInt32BigEndian(r.ReadBytes(4));
    static void WriteInt(BinaryWriter w, int v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(b, v); w.Write(b); }

    /// <summary>UTF-8 modifié : U+0000 sur 2 octets, caractères hors BMP en deux substituts de 3 octets.</summary>
    static string ReadString(BinaryReader r)
    {
        var len = BinaryPrimitives.ReadUInt16BigEndian(r.ReadBytes(2));
        var data = r.ReadBytes(len);
        var sb = new StringBuilder(len);
        for (int i = 0; i < data.Length;)
        {
            int c = data[i];
            if (c < 0x80) { sb.Append((char)c); i++; }
            else if ((c & 0xE0) == 0xC0) { sb.Append((char)(((c & 0x1F) << 6) | (data[i + 1] & 0x3F))); i += 2; }
            else { sb.Append((char)(((c & 0x0F) << 12) | ((data[i + 1] & 0x3F) << 6) | (data[i + 2] & 0x3F))); i += 3; }
        }
        return sb.ToString();
    }

    static void WriteString(BinaryWriter w, string s)
    {
        var bytes = new List<byte>(s.Length);
        foreach (char ch in s)
        {
            if (ch is >= '\u0001' and <= '\u007F') bytes.Add((byte)ch);
            else if (ch <= '߿') { bytes.Add((byte)(0xC0 | (ch >> 6))); bytes.Add((byte)(0x80 | (ch & 0x3F))); }
            else { bytes.Add((byte)(0xE0 | (ch >> 12))); bytes.Add((byte)(0x80 | ((ch >> 6) & 0x3F))); bytes.Add((byte)(0x80 | (ch & 0x3F))); }
        }
        if (bytes.Count > ushort.MaxValue) throw new InvalidDataException("Chaîne NBT trop longue");
        Span<byte> b = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)bytes.Count);
        w.Write(b);
        w.Write(bytes.ToArray());
    }
}
