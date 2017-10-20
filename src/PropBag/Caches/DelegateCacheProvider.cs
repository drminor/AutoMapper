using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DRM.PropBag.Caches
{
    public class DelegateCacheProvider
    {
        // TODO: Use the LockingConcurrentDictionary for this cache.
        static Lazy<TypeDescBasedTConverterCache> theSingleTypeDescBasedTConverterCache;
        public static TypeDescBasedTConverterCache TypeDescBasedTConverterCache
        {
            get { return theSingleTypeDescBasedTConverterCache.Value; }
        }

        static Lazy<LockingConcurrentDictionary<Type, DoSetDelegate>> theSingleDoSetDelegateCache;
        internal static LockingConcurrentDictionary<Type, DoSetDelegate> DoSetDelegateCache
        {
            get { return theSingleDoSetDelegateCache.Value; }
        }

        static DelegateCacheProvider()
        {
            theSingleTypeDescBasedTConverterCache = new Lazy<TypeDescBasedTConverterCache>(() => new TypeDescBasedTConverterCache(), LazyThreadSafetyMode.PublicationOnly);

            Func<Type, DoSetDelegate> valueFactory = DRM.PropBag.PropBagBase.GenericMethodTemplates.GetDoSetDelegate;
            theSingleDoSetDelegateCache = 
                new Lazy<LockingConcurrentDictionary<Type, DoSetDelegate>>
                (
                    () => new LockingConcurrentDictionary<Type, DoSetDelegate>(valueFactory),
                    LazyThreadSafetyMode.PublicationOnly
                );
        }


        private DelegateCacheProvider() { } // Mark as private to disallow creation of an instance of this class.
    }
}
