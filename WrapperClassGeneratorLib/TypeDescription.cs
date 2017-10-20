using DRM.TypeSafePropertyBag;
using System;
using System.Collections.Generic;
using System.Linq;


namespace DRMWrapperClassGenLib
{
    public struct TypeDescription<T> : IEquatable<TypeDescription<T>> where T: ITypeSafePropBag
    {
        public TypeDescription(string name) : this(name, PropertyDescription.Empty)
        {
        }

        public TypeDescription(string name, IEnumerable<PropertyDescription> additionalProperties)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            //Type = type ?? throw new ArgumentNullException(nameof(type));
            AdditionalProperties = additionalProperties?.ToArray() ?? throw new ArgumentNullException(nameof(additionalProperties));
        }

        public string Name { get; }
        public Type Type { get { return typeof(T); } }

        public PropertyDescription[] AdditionalProperties { get; }

        public Dictionary<string, Type> TypeDefs
        {
            get
            {
                Dictionary<string, Type> result =
                    AdditionalProperties
                    .Select(x => new KeyValuePair<string, Type>(x.Name, x.Type))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);

                return result;
            }
        }

        public override int GetHashCode()
        {
            return GenerateHash.CustomHash(Name.GetHashCode(), Type.GetHashCode());
            //var hashCode = Type.GetHashCode();
            //foreach (var property in AdditionalProperties)
            //{
            //    hashCode = GenerateHash.CustomHash(hashCode, property.GetHashCode());
            //}
            //return hashCode;
        }

        public override bool Equals(object other) => other is TypeDescription<T> && Equals((TypeDescription<T>)other);

        public bool Equals(TypeDescription<T> other) => Name == other.Name && Type == other.Type; // && AdditionalProperties.SequenceEqual(other.AdditionalProperties);

        public static bool operator ==(TypeDescription<T> left, TypeDescription<T> right) => left.Equals(right);

        public static bool operator !=(TypeDescription<T> left, TypeDescription<T> right) => !left.Equals(right);
    }

}
