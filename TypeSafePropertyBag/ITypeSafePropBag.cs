using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DRM.TypeSafePropertyBag
{
    public interface ITypeSafePropBag
    {
        //Dictionary<string, ValPlusType> NamedValuesWithType { get; }
        //ReadOnlyDictionary<string, Type> TypeDefs { get; }
        Type GetTypeOfProperty([CallerMemberName] string propertyName = null);
            
        object GetItWithNoType([CallerMemberName] string propertyName = null);

        bool SetItWithType(object value, Type propertyType, [CallerMemberName] string propertyName = null);

        bool SetItWithNoType(object value, [CallerMemberName] string propertyName = null);
    }
}
