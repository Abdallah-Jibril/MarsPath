namespace MarsPath
{
    /// <summary>Reject incomplete HTTP/cache images before the tolerant/native
    /// decoder can turn truncated data into missing blocks or repeated rows.</summary>
    public static class MarsCtxImageIntegrity
    {
        private static readonly uint[] crcTable=MakeTable();
        public static bool IsCompleteImage(byte[] data)
        {
            if(data==null||data.Length<32)return false;
            if(data[0]==255&&data[1]==216)return data[data.Length-2]==255&&data[data.Length-1]==217;
            if(data[0]!=137||data[1]!=80||data[2]!=78||data[3]!=71||data[4]!=13||data[5]!=10||data[6]!=26||data[7]!=10)return false;
            int offset=8;bool header=false,pixels=false;
            while(offset<=data.Length-12)
            {
                uint length=Big(data,offset),kind=Big(data,offset+4);long end=offset+12L+length;
                if(end>data.Length||length>int.MaxValue)return false;
                if(!header){if(kind!=0x49484452||length!=13)return false;header=true;}
                uint crc=0xffffffff;int until=offset+8+(int)length;
                for(int i=offset+4;i<until;i++)crc=crcTable[(int)((crc^data[i])&255)]^(crc>>8);
                if((crc^0xffffffff)!=Big(data,until))return false;
                if(kind==0x49444154)pixels=true;
                if(kind==0x49454e44)return length==0&&header&&pixels&&end==data.Length;
                offset=(int)end;
            }
            return false;
        }
        private static uint Big(byte[] b,int p)=>((uint)b[p]<<24)|((uint)b[p+1]<<16)|((uint)b[p+2]<<8)|b[p+3];
        private static uint[] MakeTable()
        {var result=new uint[256];for(int i=0;i<256;i++){uint n=(uint)i;for(int bit=0;bit<8;bit++)n=(n&1)!=0?0xedb88320^(n>>1):n>>1;result[i]=n;}return result;}
    }
}
