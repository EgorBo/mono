using System;

namespace IntrinsicsPlayground
{
	unsafe class Program
	{
		static unsafe void Main(string[] args)
		{

			Console.WriteLine("1.79769313486231E+308 - expected");
			Console.WriteLine(NumberToDouble());

			long l = 9218868437227405282;
			//var aa = BitConverter.Int64BitsToDouble(l);
			Console.WriteLine(*(double*)&l);
		}


		private static unsafe double NumberToDouble()
		{
			ulong val = 18446744073709490986;
			int exp = 1024;


			// round & scale down
			if (((int)val & (1 << 10)) != 0)
			{
				// IEEE round to even
				ulong tmp = val + ((1 << 10) - 1) + (ulong)(((int)val >> 11) & 1);
				if (tmp < val)
				{
					// overflow
					tmp = (tmp >> 1) | 0x8000000000000000;
					exp += 1;
				}
				val = tmp;
			}

			// return the exponent to a biased state
			exp += 0x3FE;

			// handle overflow, underflow, "Epsilon - 1/2 Epsilon", denormalized, and the normal case
			if (exp <= 0)
			{
				if (exp == -52 && (val >= 0x8000000000000058))
				{
					// round X where {Epsilon > X >= 2.470328229206232730000000E-324} up to Epsilon (instead of down to zero)
					val = 0x0000000000000001;
				}
				else if (exp <= -52)
				{
					// underflow
					val = 0;
				}
				else
				{
					// denormalized
					val >>= (-exp + 11 + 1);
				}
			}
			else if (exp >= 0x7FF)
			{
				Console.WriteLine("OVERFLOW!");
				// overflow
				val = 0x7FF0000000000000;
			}
			else
			{
				//Console.WriteLine("NORMAL!");
				// normal postive exponent case
				val = ((ulong)exp << 52) + ((val >> 11) & 0x000FFFFFFFFFFFFF);
			}

			if (false) //sign
				val |= 0x8000000000000000;


			ulong gg = 9218868437227405282;
			double gdd = *(double*)&gg;

			Console.WriteLine($"|_ {gg}={gdd}   val={val}, exp={exp},  true? {gg == val}");

			//double dd = BitConverter.Int64BitsToDouble((long)val);
			double dd =  *(double*)&val;
			Console.WriteLine($"dd={dd}");
			return dd;
		}
	}
}
