using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace DRM.PropBag.Caches
{
    internal delegate bool DoSetDelegate(object value, PropBagBase target, string propertyName, object prop);

    [Serializable]
    internal class DoSetDelegateCache : Dictionary<Type, DoSetDelegate>
    {
        public DoSetDelegateCache()
        {
        }

        protected DoSetDelegateCache(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            
        }
    }

}
