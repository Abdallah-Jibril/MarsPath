using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Official offline 64 ppd MOLA. Small exact row segments keep
    /// sparse planetary queries bounded without loading whole latitude bands.
    /// Pixel-centre interpolation; no synthetic relief.</summary>
    public static class MarsGlobalMola64
    {
        private const int Width=11520,Height=5760,Segment=128,CacheLimit=16384;
        private static bool initialized,available;
        private static readonly FileStream[] files=new FileStream[4];
        private sealed class Block{public int key;public readonly byte[] bytes=new byte[Segment*2];}
        private static readonly Dictionary<int,LinkedListNode<Block>> segments=new Dictionary<int,LinkedListNode<Block>>();
        private static readonly LinkedList<Block> recent=new LinkedList<Block>();
        // Cumulative counters support runtime regression checks. A cache miss
        // transfers only 256 bytes; cached raster payload is at most 4 MiB.
        public static long BytesRead{get;private set;}
        public static long CacheMisses{get;private set;}
        public static int CachedSegments=>segments.Count;
        public static bool Available{get{Initialize();return available;}}
        private static void Initialize()
        {
            if(initialized)return;initialized=true;
            // Android StreamingAssets live inside the APK and cannot be opened as seekable files.
            // The packaged 16 ppd grid still covers global routes and Jezero uses its own 1 m DTM.
            if(Application.platform==RuntimePlatform.Android)return;
            string[] names={"megt90n000gb","megt90n180gb","megt00n000gb","megt00n180gb"};
            try
            {
                // Disable FileStream's read-ahead buffer: sparse samples should
                // not turn a 256-byte request into an unrelated 64 KiB read.
                for(int i=0;i<4;i++){string path=Path.Combine(Application.streamingAssetsPath,"Mola64",names[i]+".img");if(new FileInfo(path).Length!=132710400)throw new IOException("Incomplete MOLA raster");files[i]=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,1,FileOptions.RandomAccess);}
                available=true;Application.quitting+=Close;
            }
            catch(IOException){Close();}
        }
        private static void Close(){foreach(var f in files)f?.Dispose();segments.Clear();recent.Clear();available=false;}
        private static short Read(int x,int y)
        {
            int tile=(y>=Height?2:0)+(x>=Width?1:0),lx=x%Width,ly=y%Height,segment=lx/Segment;
            int key=(tile*Height+ly)*(Width/Segment)+segment;
            if(!segments.TryGetValue(key,out LinkedListNode<Block> node))
            {
                // Reuse the least-recent block after the cache fills. Lookup,
                // eviction and promotion remain O(1), with no per-miss GC churn.
                if(segments.Count>=CacheLimit)
                {
                    node=recent.Last;recent.RemoveLast();segments.Remove(node.Value.key);
                }
                else node=new LinkedListNode<Block>(new Block());
                Block block=node.Value;block.key=key;
                FileStream file=files[tile];file.Position=((long)ly*Width+segment*Segment)*2;
                int n=0;while(n<block.bytes.Length)
                {
                    int got=file.Read(block.bytes,n,block.bytes.Length-n);
                    if(got==0)throw new EndOfStreamException("MOLA elevation row truncated");
                    n+=got;BytesRead+=got;
                }
                CacheMisses++;segments.Add(key,node);recent.AddFirst(node);
            }
            else if(node!=recent.First){recent.Remove(node);recent.AddFirst(node);}
            int at=(lx%Segment)*2;return(short)(node.Value.bytes[at]<<8|node.Value.bytes[at+1]);
        }
        public static bool TryElevation(float latitude,float longitude,out float height)
        {
            height=0;if(!Available)return false;
            double lon=longitude;lon-=Math.Floor(lon/360)*360;
            double column=lon*64-.5,row=Math.Max(0,Math.Min(11519,(90.0-latitude)*64-.5));
            int rawX=(int)Math.Floor(column),x=(rawX+23040)%23040,y=(int)Math.Floor(row),x1=(x+1)%23040,y1=Math.Min(11519,y+1);
            float tx=(float)(column-rawX),ty=(float)(row-y);
            height=Mathf.Lerp(Mathf.Lerp(Read(x,y),Read(x1,y),tx),Mathf.Lerp(Read(x,y1),Read(x1,y1),tx),ty);return true;
        }
    }
}
