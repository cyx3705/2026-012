using System;

namespace OneHistory.ColdBrewTwin
{
    public sealed class AnnularChannelProfile
    {
        public readonly struct Slice
        {
            public readonly double Y, Inner, Outer;
            public Slice(double y,double inner,double outer) { Y=y; Inner=inner; Outer=outer; }
        }
        readonly Slice[] slices;
        readonly double[] volumes;
        public int Count => slices.Length;
        public Slice this[int index] => slices[index];
        public double Capacity => volumes[volumes.Length-1];
        public AnnularChannelProfile(Slice[] source)
        {
            if(source==null || source.Length<2) throw new ArgumentException("Channel profile needs at least two slices.");
            slices=(Slice[])source.Clone(); volumes=new double[slices.Length];
            for(int i=0;i<slices.Length;i++)
            {
                var s=slices[i];
                if(double.IsNaN(s.Y+s.Inner+s.Outer) || double.IsInfinity(s.Y+s.Inner+s.Outer) ||
                    s.Inner<=0 || s.Outer<=s.Inner || (i>0 && s.Y<=slices[i-1].Y))
                    throw new ArgumentException("Invalid annular channel slice.");
                if(i>0) volumes[i]=volumes[i-1]+Segment(slices[i-1],s);
            }
        }
        static double Segment(Slice a,Slice b) => Math.PI*(b.Y-a.Y)/3*1000*
            (a.Outer*a.Outer+a.Outer*b.Outer+b.Outer*b.Outer-a.Inner*a.Inner-a.Inner*b.Inner-b.Inner*b.Inner);
        public Slice At(double y)
        {
            if(y<=slices[0].Y) return slices[0];
            if(y>=slices[Count-1].Y) return slices[Count-1];
            int low=0,high=Count-1;
            while(high-low>1) { int mid=(low+high)/2; if(slices[mid].Y<=y) low=mid; else high=mid; }
            var a=slices[low]; var b=slices[high]; double t=(y-a.Y)/(b.Y-a.Y);
            return new Slice(y,a.Inner+(b.Inner-a.Inner)*t,a.Outer+(b.Outer-a.Outer)*t);
        }
        public double VolumeBelow(double y)
        {
            if(y<=slices[0].Y) return 0;
            if(y>=slices[Count-1].Y) return Capacity;
            int low=0,high=Count-1;
            while(high-low>1) { int mid=(low+high)/2; if(slices[mid].Y<=y) low=mid; else high=mid; }
            return volumes[low]+Segment(slices[low],At(y));
        }
        public double LevelForVolume(double litres)
        {
            if(litres<=0) return slices[0].Y;
            if(litres>=Capacity) return slices[Count-1].Y;
            int low=0,high=Count-1;
            while(high-low>1) { int mid=(low+high)/2; if(volumes[mid]<=litres) low=mid; else high=mid; }
            double bottom=slices[low].Y,top=slices[high].Y;
            for(int i=0;i<20;i++)
            {
                double mid=(bottom+top)*.5;
                if(volumes[low]+Segment(slices[low],At(mid))<litres) bottom=mid; else top=mid;
            }
            return (bottom+top)*.5;
        }
    }
}
