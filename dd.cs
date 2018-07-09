using System;

namespace IntrinsicsPlayground
{
    unsafe class Program
    {
        static unsafe void Main(string[] args)
        {
	        ulong l = 9218868437227405282;
	        double dd = *(double*) &l;

			Console.WriteLine(dd);
        }
	}
}
