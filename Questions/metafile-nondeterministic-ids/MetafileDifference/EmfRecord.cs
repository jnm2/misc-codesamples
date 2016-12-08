using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MetafileDifference
{
    internal struct EmfRecord
    {
        public readonly EmfPlusRecordType Type;
        public readonly ushort Flags;
        public readonly byte[] Data;

        public EmfRecord(EmfPlusRecordType type, ushort flags, byte[] data)
        {
            Type = type;
            Flags = flags;
            Data = data;
        }

        public static IReadOnlyList<EmfRecord> GetMetafileRecords(Metafile image)
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
                return GetMetafileRecords(image, g);
        }

        public static IReadOnlyList<EmfRecord> GetMetafileRecords(Metafile image, Graphics graphics)
        {
            var r = new List<EmfRecord>();

            graphics.EnumerateMetafile(image, default(Point), (type, flags, size, data, callbackData) =>
            {
                byte[] array;
                if (data == IntPtr.Zero)
                {
                    array = null;
                }
                else
                {
                    array = new byte[size];
                    Marshal.Copy(data, array, 0, array.Length);
                }
                r.Add(new EmfRecord(type, checked((ushort)flags), array));
                return true;
            });

            return r;
        }
        
        public override string ToString()
        {
            var sb = new StringBuilder();

            switch (Type)
            {
                case EmfPlusRecordType.SetAntiAliasMode:
                    sb.Append("SetAntiAliasMode, ");
                    sb.Append((Flags & 0x1) == 0 ? "off, mode " : "on, mode ");
                    sb.Append((SmoothingMode)((Flags >> 1) & 0x7F));
                    break;
                case EmfPlusRecordType.DrawLines:
                    sb.Append("DrawLines, pen id ").Append((byte)Flags);
                    sb.Append((Flags & 0x800) != 0 ? ", relative" :
                        (Flags & 0x4000) != 0 ? ", absolute int16" : ", absolute float32");
                    sb.Append((Flags & 0x2000) != 0 ? ", closed" : ", open");
                    sb.Append(", 0x").Append(Data.ToHexString());
                    break;
                case EmfPlusRecordType.Object:
                    sb.Append("Object, id ").Append((byte)Flags);
                    var objectType = (ObjectType)((Flags >> 8) & 0x7F);
                    sb.Append(", type ").Append(objectType);
                    if ((Flags & 0x8000) != 0) sb.Append(", continued");

                    switch (objectType)
                    {
                        case ObjectType.Region:
                            sb.Append(", ");
                            AppendRegion(sb, Data, 8);
                            break;
                        default:
                            sb.Append(", 0x").Append(Data.ToHexString());
                            break;
                    }
                    break;
                case EmfPlusRecordType.SetClipRegion:
                    sb.Append("SetClipRegion, region id ").Append((byte)Flags);
                    sb.Append(", mode ").Append((CombineMode)((Flags >> 8) & 0xF));
                    break;
                case EmfPlusRecordType.DrawString:
                {
                    sb.Append("DrawString, font id ").Append((byte)Flags);
                    var isBrushIdActuallyColor = (Flags & 0x8000) != 0;

                    var brushId = BitConverter.ToUInt32(Data, 0);
                    if (isBrushIdActuallyColor)
                        sb.Append(", color #").Append(brushId.ToString("X8"));
                    else
                        sb.Append(", brush id ").Append(brushId);

                    sb.Append(", format id ").Append(BitConverter.ToUInt32(Data, 4));
                    sb.Append(", ");
                    AppendRectFloat32(sb, Data, 12);
                    var stringLength = BitConverter.ToUInt32(Data, 8);
                    sb.Append(", text ").Append(Encoding.Unicode.GetString(Data, 28, checked((int)stringLength * 2)));
                    break;
                }
                case EmfPlusRecordType.FillPolygon:
                {
                    sb.Append("FillPolygon");
                    var isBrushIdActuallyColor = (Flags & 0x8000) != 0;
                    var brushId = BitConverter.ToUInt32(Data, 0);
                    if (isBrushIdActuallyColor)
                        sb.Append(", color #").Append(brushId.ToString("X8"));
                    else
                        sb.Append(", brush id ").Append(brushId);

                    sb.Append((Flags & 0x800) != 0 ? ", relative" :
                        (Flags & 0x4000) != 0 ? ", absolute int16" : ", absolute float32");

                    sb.Append(", 0x").Append(Data.Skip(4).ToHexString());
                    break;
                }
                case EmfPlusRecordType.FillEllipse:
                {
                    sb.Append("FillEllipse");
                    var isBrushIdActuallyColor = (Flags & 0x8000) != 0;
                    var brushId = BitConverter.ToUInt32(Data, 0);
                    if (isBrushIdActuallyColor)
                        sb.Append(", color #").Append(brushId.ToString("X8"));
                    else
                        sb.Append(", brush id ").Append(brushId);

                    sb.Append((Flags & 0x4000) != 0 ? ", int16" : ", float32");

                    sb.Append(", 0x").Append(Data.Skip(4).ToHexString());
                    break;
                }
                case EmfPlusRecordType.DrawEllipse:
                    sb.Append("DrawEllipse, pen id ").Append((byte)Flags);
                    sb.Append((Flags & 0x4000) != 0 ? ", int16" : ", float32");
                    sb.Append(", 0x").Append(Data.ToHexString());
                    break;
                case EmfPlusRecordType.EmfHeader:
                    sb.Append("EmfHeader, bounds(left: ").Append(BitConverter.ToInt32(Data, 0));
                    sb.Append(", top: ").Append(BitConverter.ToInt32(Data, 4));
                    sb.Append(", right: ").Append(BitConverter.ToInt32(Data, 8));
                    sb.Append(", bottom: ").Append(BitConverter.ToInt32(Data, 12));
                    sb.Append("), frame(left: ").Append(BitConverter.ToInt32(Data, 16));
                    sb.Append(", top: ").Append(BitConverter.ToInt32(Data, 20));
                    sb.Append(", right: ").Append(BitConverter.ToInt32(Data, 24));
                    sb.Append(", bottom: ").Append(BitConverter.ToInt32(Data, 28));
                    sb.Append("), [...]");
                    break;
                case EmfPlusRecordType.FillRegion:
                {
                    sb.Append("FillRegion, region id ").Append((byte)Flags);
                    var isBrushIdActuallyColor = (Flags & 0x8000) != 0;
                    var brushId = BitConverter.ToUInt32(Data, 0);
                    if (isBrushIdActuallyColor)
                        sb.Append(", color #").Append(brushId.ToString("X8"));
                    else
                        sb.Append(", brush id ").Append(brushId);
                    break;
                }
                case EmfPlusRecordType.DrawRects:
                    sb.Append("DrawRects, pen id ").Append((byte)Flags);
                    var isCompressed = (Flags & 0x4000) != 0;

                    var count = BitConverter.ToUInt32(Data, 0);
                    for (var i = 0; i < count; i++)
                    {
                        sb.Append(i == 0 ? ": (" : ", (");
                        if (isCompressed)
                            AppendRectInt16(sb, Data, 4 + i * 2);
                        else
                            AppendRectFloat32(sb, Data, 4 + i * 4);
                        sb.Append(')');
                    }
                    break;
                default:
                    sb.Append(Type.ToString().PadRight(27));
                    sb.Append(Convert.ToString((uint)Flags, 2).PadLeft(16)).Append(" 0x");
                    if (Data != null) sb.Append(Data.ToHexString());
                    break;
            }

            return sb.ToString();
        }

        /// <returns>Length of the node data</returns>
        private static int AppendRegion(StringBuilder sb, byte[] data, int index)
        {
            var type = (RegionNodeDataType)BitConverter.ToUInt32(data, index);
            switch (type)
            {
                case RegionNodeDataType.And:
                case RegionNodeDataType.Or:
                case RegionNodeDataType.Xor:
                case RegionNodeDataType.Exclude:
                case RegionNodeDataType.Complement:
                    sb.Append('(');
                    var leftLength = AppendRegion(sb, data, index + 4);
                    sb.Append(' ').Append(type).Append(' ');
                    var rightLength = AppendRegion(sb, data, index + 4 + leftLength);
                    sb.Append(')');
                    return 4 + leftLength + rightLength;
                case RegionNodeDataType.Empty:
                case RegionNodeDataType.Infinite:
                    sb.Append(type);
                    return 4;
                case RegionNodeDataType.Rect:
                    sb.Append("Rect(");
                    AppendRectFloat32(sb, data, index + 4);
                    sb.Append(')');
                    return 20;
                case RegionNodeDataType.Path:
                    var length = BitConverter.ToInt32(data, index + 4);
                    sb.Append("Path(");
                    AppendPath(sb, data, index + 8);
                    sb.Append(')');
                    return checked(length + 8);
                default:
                    throw new NotImplementedException(type.ToString());
            }
        }

        private static void AppendPath(StringBuilder sb, byte[] data, int index)
        {
            var numPoints = BitConverter.ToInt32(data, index + 4);
            sb.Append("0x").Append(data.Skip(index + 4).Take(8 + (4 * numPoints)).ToHexString());
        }

        private static void AppendRectFloat32(StringBuilder sb, byte[] data, int index)
        {
            sb.Append("x: ").Append(BitConverter.ToSingle(data, index));
            sb.Append(", y: ").Append(BitConverter.ToSingle(data, index + 4));
            sb.Append(", width: ").Append(BitConverter.ToSingle(data, index + 8));
            sb.Append(", height: ").Append(BitConverter.ToSingle(data, index + 12));
        }
        private static void AppendRectInt16(StringBuilder sb, byte[] data, int index)
        {
            sb.Append("x: ").Append(BitConverter.ToInt16(data, index));
            sb.Append(", y: ").Append(BitConverter.ToInt16(data, index + 2));
            sb.Append(", width: ").Append(BitConverter.ToInt16(data, index + 4));
            sb.Append(", height: ").Append(BitConverter.ToInt16(data, index + 6));
        }

        private enum RegionNodeDataType : uint
        {
            And = 0x00000001,
            Or = 0x00000002,
            Xor = 0x00000003,
            Exclude = 0x00000004,
            Complement = 0x00000005,
            Rect = 0x10000000,
            Path = 0x10000001,
            Empty = 0x10000002,
            Infinite = 0x10000003
        }

        private enum SmoothingMode
        {
            Default = 0x00,
            HighSpeed = 0x01,
            HighQuality = 0x02,
            None = 0x03,
            AntiAlias8x4 = 0x04,
            AntiAlias8x8 = 0x05
        }

        private enum ObjectType
        {
            Invalid = 0x00000000,
            Brush = 0x00000001,
            Pen = 0x00000002,
            Path = 0x00000003,
            Region = 0x00000004,
            Image = 0x00000005,
            Font = 0x00000006,
            StringFormat = 0x00000007,
            ImageAttributes = 0x00000008,
            CustomLineCap = 0x00000009
        }

        private enum CombineMode
        {
            Replace = 0x00000000,
            Intersect = 0x00000001,
            Union = 0x00000002,
            XOR = 0x00000003,
            Exclude = 0x00000004,
            Complement = 0x00000005
        }
    }
}
