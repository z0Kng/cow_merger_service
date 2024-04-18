namespace cow_merger_service;
using System;
using System.Linq;

public class ByteArrayHelper
{
    public static void Or(ref byte[] data, byte[] data2){
        for(int i = 0; i< data.Length; i++){
            data[i] = (byte) (data[i] | data2[i]);
        }
    }
    public static bool checkBit(Span<byte> bitfield, int n)
    {
        return ((bitfield[n / 8] >> (n % 8)) & 1) > 0;
    }
        
    public static string ByteArrayToString(byte[] a)
    {
        return string.Join(" ", a.Select(x => Convert.ToString(x, 2).PadLeft(8, '0')));
    }
}
