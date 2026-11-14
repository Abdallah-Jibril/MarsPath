using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using BitMiracle.LibTiff.Classic;
using UnityEngine;
using UnityEngine.Networking;

namespace MarsPath
{
    /// <summary>Bounded HTTP-range COG reader. Reads actual source tiles with the
    /// managed TIFF codec (including floating-point predictors); no GIS install.</summary>
    public static class MarsCogWindow
    {
        public sealed class Window
        {
            public int width,height;public double west,north,dx,dy;public float[] values;
            public float spacing;public string source;
            public double radius,longitude;public bool sinusoidal;
        }
        private sealed class Block{public long start;public byte[] data;}
        private sealed class Sparse:TiffStream
        {
            public long length,position;public readonly List<Block> blocks=new List<Block>();
            public override long Size(object client)=>length;
            public override long Seek(object client,long offset,SeekOrigin origin)
            {position=origin==SeekOrigin.Begin?offset:origin==SeekOrigin.Current?position+offset:length+offset;return position;}
            public override int Read(object client,byte[] buffer,int offset,int count)
            {
                int read=0;
                while(read<count)
                {
                    Block found=null;foreach(Block b in blocks)if(position>=b.start&&position<b.start+b.data.Length){found=b;break;}
                    if(found==null)break;int size=(int)Math.Min(count-read,found.start+found.data.Length-position);
                    Buffer.BlockCopy(found.data,(int)(position-found.start),buffer,offset+read,size);read+=size;position+=size;
                }
                return read;
            }
            public override void Close(object client){}
        }
        private sealed class BufferLimit:DownloadHandlerScript
        {
            private readonly byte[] bytes;private int used;
            public BufferLimit(int size):base(new byte[32768]){bytes=new byte[size];}
            protected override bool ReceiveData(byte[] data,int count){if(count>bytes.Length-used)return false;Buffer.BlockCopy(data,0,bytes,used,count);used+=count;return true;}
            protected override byte[] GetData(){byte[] result=new byte[used];Buffer.BlockCopy(bytes,0,result,0,used);return result;}
        }
        private sealed class Job{public long offset;public int count;public byte[] data;public UnityWebRequest request;public UnityWebRequestAsyncOperation op;}
        public static string LastError{get;private set;}
        private static string Cache=>Path.Combine(Application.persistentDataPath,"OfficialTerrain","v1","cog");
        private static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").Substring(0,24);}
        private static string ChunkPath(string url,long start,int count)=>Path.Combine(Cache,Hash(url)+"_"+start+"_"+count+".bin");
        private static IEnumerator Fetch(string url,List<Job> jobs,Func<bool> stop,Action<float> progress,Action<long> fileSize)
        {
            Directory.CreateDirectory(Cache);int next=0,done=0;var active=new List<Job>();
            try
            {
                while(done<jobs.Count)
                {
                    if(stop())yield break;
                    while(active.Count<8&&next<jobs.Count)
                    {
                        Job j=jobs[next++];string path=ChunkPath(url,j.offset,j.count);
                        if(File.Exists(path))try{byte[] b=File.ReadAllBytes(path);if(b.Length==j.count){j.data=b;done++;continue;}}catch(IOException){}
                        j.request=new UnityWebRequest(url,"GET",new BufferLimit(j.count),null);j.request.timeout=22;
                        j.request.SetRequestHeader("Range",$"bytes={j.offset}-{j.offset+j.count-1}");j.op=j.request.SendWebRequest();active.Add(j);
                    }
                    for(int i=active.Count-1;i>=0;i--)
                    {
                        Job j=active[i];if(!j.op.isDone)continue;
                        string range=j.request.GetResponseHeader("Content-Range")??"";
                        if(j.request.result==UnityWebRequest.Result.Success&&j.request.responseCode==206&&range.StartsWith("bytes "+j.offset+"-"))
                        {
                            byte[] b=j.request.downloadHandler.data;if(b.Length==j.count){j.data=b;try{File.WriteAllBytes(ChunkPath(url,j.offset,j.count),b);}catch(IOException){}}
                            int slash=range.LastIndexOf('/');if(slash>0&&long.TryParse(range.Substring(slash+1),out long length))fileSize(length);
                        }
                        if(j.data==null)LastError="Official terrain byte range unavailable: "+j.request.error;
                        j.request.Dispose();j.request=null;active.RemoveAt(i);done++;
                    }
                    progress?.Invoke(done/(float)Math.Max(1,jobs.Count));yield return null;
                }
            }
            finally{foreach(Job j in active)if(j.request!=null){j.request.Abort();j.request.Dispose();j.request=null;}}
        }
        public static IEnumerator Read(string url,GeoPoint focus,float radiusMeters,Func<bool> cancelled,Action<float> progress,Action<Window> complete,bool hrscSinusoidal=false)
        {
            LastError="";float started=Time.realtimeSinceStartup;
            Func<bool> stop=()=>Time.realtimeSinceStartup-started>65||(cancelled!=null&&cancelled());
            bool scienceTeam=url.StartsWith("https://maps.planet.fu-berlin.de/mex4/",StringComparison.Ordinal);
            if(!url.StartsWith("https://astrogeo-ard.s3-us-west-2.amazonaws.com/",StringComparison.Ordinal)&&!scienceTeam){LastError="Unrecognized official terrain host";complete(null);yield break;}
            if(scienceTeam!=hrscSinusoidal){LastError="Scientific terrain projection must be explicitly selected";complete(null);yield break;}
            var header=new Job{offset=0,count=262144};long length=0;string sizePath=Path.Combine(Cache,Hash(url)+".size");
            Directory.CreateDirectory(Cache);if(File.Exists(sizePath))long.TryParse(File.ReadAllText(sizePath),out length);
            yield return Fetch(url,new List<Job>{header},stop,p=>progress?.Invoke(p*.05f),n=>length=n);
            if(header.data==null||length<=0||stop()){complete(null);yield break;}
            try{File.WriteAllText(sizePath,length.ToString(System.Globalization.CultureInfo.InvariantCulture));}catch(IOException){}
            var stream=new Sparse{length=length};stream.blocks.Add(new Block{start=0,data=header.data});
            Tiff image=null;List<Job> jobs=null;long[] offsets=null;int[] counts=null;int tw=0,th=0,columns=0,x0=0,y0=0,x1=0,y1=0,bits=0,format=0;
            double west=0,north=0,dx=0,dy=0,nativeRadius=3396190,nativeLongitude=0;Window result=null;string error=null;
            try
            {
                image=Tiff.ClientOpen("Official Mars COG","r",stream,stream);
                if(image==null||!image.IsTiled())throw new InvalidDataException("Terrain TIFF must be tiled");
                int width=image.GetField(TiffTag.IMAGEWIDTH)[0].ToInt(),height=image.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
                tw=image.GetField(TiffTag.TILEWIDTH)[0].ToInt();th=image.GetField(TiffTag.TILELENGTH)[0].ToInt();
                bits=image.GetFieldDefaulted(TiffTag.BITSPERSAMPLE)[0].ToInt();format=image.GetFieldDefaulted(TiffTag.SAMPLEFORMAT)[0].ToInt();
                if(width<=0||height<=0||tw<16||th<16||tw>2048||th>2048||image.GetFieldDefaulted(TiffTag.SAMPLESPERPIXEL)[0].ToInt()!=1||bits!=32&&bits!=16&&bits!=8)throw new InvalidDataException("Unsupported bounded terrain TIFF layout");
                GeoTransform(header.data,out west,out north,out dx,out dy);
                if(hrscSinusoidal)HrscProjection(header.data,out nativeRadius,out nativeLongitude);
                double longitude=focus.longitude,center=hrscSinusoidal?nativeLongitude:(west+width*dx*.5)/nativeRadius*180/Math.PI;
                while(longitude-center>180)longitude-=360;while(longitude-center< -180)longitude+=360;
                double latitude=focus.latitude*Math.PI/180,easting=hrscSinusoidal?nativeRadius*Math.Cos(latitude)*(longitude-nativeLongitude)*Math.PI/180:longitude*Math.PI/180*nativeRadius;
                int cx=(int)Math.Floor((easting-west)/dx),cy=(int)Math.Floor((latitude*nativeRadius-north)/dy);
                int rx=Math.Min(1024,(int)Math.Ceiling(radiusMeters/Math.Max(.1,dx))),ry=Math.Min(1024,(int)Math.Ceiling(radiusMeters/Math.Max(.1,-dy)));
                x0=Math.Max(0,(cx-rx)/tw*tw);y0=Math.Max(0,(cy-ry)/th*th);x1=Math.Min(width,((cx+rx)/tw+1)*tw);y1=Math.Min(height,((cy+ry)/th+1)*th);
                if(x1<=x0||y1<=y0||x1-x0>3072||y1-y0>3072)throw new InvalidDataException("Invalid terrain focus window");
                offsets=image.GetField(TiffTag.TILEOFFSETS)[0].TolongArray();counts=image.GetField(TiffTag.TILEBYTECOUNTS)[0].ToIntArray();columns=(width+tw-1)/tw;jobs=new List<Job>();long transfer=0;
                for(int y=y0/th;y<=(y1-1)/th;y++)for(int x=x0/tw;x<=(x1-1)/tw;x++)
                {int index=y*columns+x;if(index<0||index>=offsets.Length||counts[index]<=0||counts[index]>16*1024*1024)throw new InvalidDataException("Invalid source tile byte range");jobs.Add(new Job{offset=offsets[index],count=counts[index]});transfer+=counts[index];}
                if(transfer>64L*1024*1024)throw new InvalidDataException("High-resolution window exceeds transfer budget");
            }
            catch(Exception e){error=e.Message;}
            if(error!=null){image?.Dispose();LastError=error;complete(null);yield break;}
            yield return Fetch(url,jobs,stop,p=>progress?.Invoke(.05f+p*.85f),n=>{});
            if(stop()||jobs.Exists(j=>j.data==null)){image.Dispose();complete(null);yield break;}
            try
            {
                foreach(Job j in jobs)stream.blocks.Add(new Block{start=j.offset,data=j.data});
                result=new Window{width=x1-x0,height=y1-y0,west=west+x0*dx,north=north+y0*dy,dx=dx,dy=dy,spacing=(float)dx,source=url,values=new float[(x1-x0)*(y1-y0)],radius=nativeRadius,longitude=nativeLongitude,sinusoidal=hrscSinusoidal};
                for(int i=0;i<result.values.Length;i++)result.values[i]=float.NaN;
                byte[] tile=new byte[image.TileSize()];
                for(int ty=y0/th;ty<=(y1-1)/th;ty++)for(int tx=x0/tw;tx<=(x1-1)/tw;tx++)
                {
                    if(image.ReadEncodedTile(ty*columns+tx,tile,0,tile.Length)<0)throw new InvalidDataException("Official TIFF tile failed lossless decoding");
                    for(int y=0;y<th&&ty*th+y<y1;y++)for(int x=0;x<tw&&tx*tw+x<x1;x++)
                    {
                        int offset=(y*tw+x)*(bits/8);float value=bits==32&&format==3?BitConverter.ToSingle(tile,offset):bits==16?(format==2?BitConverter.ToInt16(tile,offset):BitConverter.ToUInt16(tile,offset)):bits==8?tile[offset]:float.NaN;
                        result.values[(ty*th+y-y0)*result.width+tx*tw+x-x0]=value;
                    }
                }
            }
            catch(Exception e){LastError=e.Message;result=null;}
            finally{image.Dispose();}
            progress?.Invoke(1);complete(result);
        }
        private static void HrscProjection(byte[] b,out double radius,out double longitude)
        {
            if(b[0]!=73)throw new InvalidDataException("HRSC native TIFF byte order must be validated");
            bool big=BitConverter.ToUInt16(b,2)==43;Func<int,ulong> u64=i=>BitConverter.ToUInt64(b,i);
            int ifd=checked((int)(big?u64(8):BitConverter.ToUInt32(b,4))),count=checked((int)(big?u64(ifd):BitConverter.ToUInt16(b,ifd)));
            int keys=-1,keyCount=0,doubles=-1,doubleCount=0;
            for(int n=0;n<count;n++)
            {
                int p=ifd+(big?8:2)+n*(big?20:12),tag=BitConverter.ToUInt16(b,p);if(tag!=34735&&tag!=34736)continue;
                int size=checked((int)(big?u64(p+4):BitConverter.ToUInt32(b,p+4))),start=checked((int)(big?u64(p+12):BitConverter.ToUInt32(b,p+8)));
                if(tag==34735){keys=start;keyCount=size;}else{doubles=start;doubleCount=size;}
            }
            if(keys<0||doubles<0||keys+keyCount*2>b.Length||doubles+doubleCount*8>b.Length)throw new InvalidDataException("HRSC projection keys exceed bounded TIFF header");
            radius=longitude=double.NaN;bool sinusoidal=false;
            for(int i=4;i+3<keyCount;i+=4)
            {
                int p=keys+i*2,key=BitConverter.ToUInt16(b,p),location=BitConverter.ToUInt16(b,p+2),offset=BitConverter.ToUInt16(b,p+6);
                if(key==3075&&location==0)sinusoidal=offset==24;
                if(location!=34736||offset>=doubleCount)continue;
                double value=BitConverter.ToDouble(b,doubles+offset*8);
                if(key==2057)radius=value;if(key==3088||key==3080)longitude=value;
                if((key==3082||key==3083)&&Math.Abs(value)>.001)throw new InvalidDataException("HRSC false origin is unsupported");
            }
            if(!sinusoidal||double.IsNaN(radius)||double.IsNaN(longitude)||Math.Abs(radius-3396000)>1)throw new InvalidDataException("Unsupported native HRSC sinusoidal projection");
        }
        private static void GeoTransform(byte[] b,out double west,out double north,out double dx,out double dy)
        {
            bool little=b[0]==73;Func<int,ushort> u16=i=>little?(ushort)(b[i]|b[i+1]<<8):(ushort)(b[i]<<8|b[i+1]);
            Func<int,uint> u32=i=>little?BitConverter.ToUInt32(b,i):(uint)(b[i]<<24|b[i+1]<<16|b[i+2]<<8|b[i+3]);
            Func<int,ulong> u64=i=>{byte[] a=new byte[8];Buffer.BlockCopy(b,i,a,0,8);if(little!=BitConverter.IsLittleEndian)Array.Reverse(a);return BitConverter.ToUInt64(a,0);};
            bool big=u16(2)==43;int ifd=checked((int)(big?u64(8):u32(4))),count=checked((int)(big?u64(ifd):u16(ifd)));double[] scale=null,tie=null;
            for(int n=0;n<count;n++)
            {
                int p=ifd+(big?8:2)+n*(big?20:12),tag=u16(p);if(tag!=33550&&tag!=33922)continue;
                int size=checked((int)(big?u64(p+4):u32(p+4))),start=checked((int)(big?u64(p+12):u32(p+8)));
                if(size<3||size>16||start<0||start+size*8>b.Length)throw new InvalidDataException("Geographic TIFF metadata outside bounded header");
                double[] values=new double[size];for(int i=0;i<size;i++){byte[] a=new byte[8];Buffer.BlockCopy(b,start+i*8,a,0,8);if(little!=BitConverter.IsLittleEndian)Array.Reverse(a);values[i]=BitConverter.ToDouble(a,0);}
                if(tag==33550)scale=values;else tie=values;
            }
            if(scale==null||tie==null||tie.Length<6||scale[0]<=0||scale[1]<=0)throw new InvalidDataException("Missing north-up official terrain geotransform");
            dx=scale[0];dy=-scale[1];west=tie[3]-tie[0]*dx;north=tie[4]-tie[1]*dy;
        }
    }
}
