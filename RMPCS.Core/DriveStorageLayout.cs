namespace RMPCS.Core;

public sealed record DriveExtent(uint Disk, long Offset, long Length);
public static class DriveStorageLayout
{
    // Windows winioctl.h buffers use 8-byte alignment for LARGE_INTEGER in both layouts.
    // Parse only the documented prefix, after verifying returned lengths before every read.
    public static DriveExtent ParseBasic(byte[] extents, byte[] partition)
    {
        if (extents.Length < 32 || BitConverter.ToUInt32(extents) != 1 || partition.Length < 144 ||
            BitConverter.ToUInt32(partition, 24) == 0 || partition[29] != 0)
            throw new InvalidDataException("A single non-service basic data partition could not be verified.");
        uint style = BitConverter.ToUInt32(partition);
        bool basic = style == 0 && partition[32] == 7 || style == 1 &&
            new Guid(partition.AsSpan(32, 16)) == new Guid("EBD0A0A2-B9E5-4433-87C0-68B6B72699C7") && BitConverter.ToUInt64(partition, 64) == 0;
        long offset = BitConverter.ToInt64(extents, 16), length = BitConverter.ToInt64(extents, 24);
        if (!basic || offset < 0 || length <= 0 || offset > long.MaxValue - length ||
            BitConverter.ToInt64(partition, 8) != offset || BitConverter.ToInt64(partition, 16) != length)
            throw new InvalidDataException("Virtual, dynamic, recovery, hidden or ambiguous storage layouts are not supported.");
        return new(BitConverter.ToUInt32(extents, 8), offset, length);
    }
    public static bool? BooleanProperty(byte[] descriptor) => descriptor.Length >= 9 && BitConverter.ToUInt32(descriptor) >= 9 &&
        BitConverter.ToUInt32(descriptor, 4) >= 9 && descriptor[8] <= 1 ? descriptor[8] == 1 : null;
}
