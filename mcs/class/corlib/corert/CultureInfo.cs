using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;

namespace System.Globalization
{
	[Serializable]
    partial class CultureInfo
    {

		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		private extern static string get_current_locale_name ();

        static CultureInfo default_current_culture;

		internal static CultureInfo ConstructCurrentCulture ()
		{
			if (default_current_culture != null)
				return default_current_culture;

			var locale_name = get_current_locale_name ();
			CultureInfo ci = null;

			if (locale_name != null) {
				try {
					ci = CreateSpecificCulture (locale_name);
				} catch {
				}
			}

			if (ci == null) {
				ci = InvariantCulture;
			} else {
				ci._isReadOnly = true;
				ci._useUserOverride = true;
			}

			default_current_culture = ci;
			return ci;
		}
        
        private bool _useUserOverride;

        internal static CultureInfo UserDefaultCulture {
			get {
				return ConstructCurrentCulture ();
			}
		}

        internal static CultureInfo ConstructCurrentUICulture ()
		{
			return ConstructCurrentCulture ();
		}

        
		// used in runtime (icall.c) to construct CultureInfo for
		// AssemblyName of assemblies
		internal static CultureInfo CreateCulture (string name, bool reference)
		{
			return CreateCultureInfoNoThrow(name, reference);
		}

    }
}