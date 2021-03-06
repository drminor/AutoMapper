﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Linq;

using DRM.TypeSafePropertyBag;
using DRM.Inpcwv;
using DRM.PropBag.ControlModel;
using DRM.PropBag.Caches;

namespace DRM.PropBag
{
    #region Summary and Remarks

    /// <summary>
    /// The contents of this code file were designed and created by David R. Minor, Pittsboro, NC.
    /// I have chosen to provide others free access to this intellectual property using the terms set forth
    /// by the well known Code Project Open License.
    /// Please refer to the file in this same folder named CPOP.htm for the exact set of terms that govern this release.
    /// Although not included as a condition of use, I would prefer that this text, 
    /// or a similar text which covers all of the points, be included along with a copy of cpol.htm
    /// in the set of artifacts deployed with any product
    /// wherein this source code, or a derivative thereof, is used to build the product.
    /// </summary>

    /// <remarks>
    /// While writing this code, I learned much and was guided by the material found at the following locations.
    /// http://northhorizon.net/2011/the-right-way-to-do-inotifypropertychanged/ (Daniel Moore)
    /// https://codeblog.jonskeet.uk/2008/08/09/making-reflection-fly-and-exploring-delegates/ (Jon Skeet)
    /// </remarks>

    #endregion

    public class PropBagBase : IPropBag
    {
        #region Member Declarations

        // TO-DOC: Explain that the life time of any sources that listen to the events provided by this class,
        // including the events provided by the IProp instances
        // is determined by the lifetime of the instances of classes that derive from this PropBag class.

        // TO-DOC: Explain since we may subscribe to our own events we would like to have these initialized.
        // confirm that on post construction events are initialized for us if we don't
        // alternative could be that they are initialized on first assignment.
        public event PropertyChangedEventHandler PropertyChanged; // = delegate { };
        public event PropertyChangingEventHandler PropertyChanging; // = delegate { };
        public event PropertyChangedWithValsHandler PropertyChangedWithVals; // = delegate { };

        public AbstractPropFactory ThePropFactory { get; private set; }

        private readonly Dictionary<string, IPropGen> tVals;

        //private readonly Dictionary<Type, DoSetDelegate> doSetDelegateDict;

        public PropBagTypeSafetyMode TypeSafetyMode { get; private set; }


        /// <summary>
        /// If true, attempting to set a property for which no call to AddProp has been made, will cause an exception to thrown.
        /// </summary>
        private bool AllPropsMustBeRegistered;

        /// <summary>
        /// If not true, attempting to set a property, not previously set with a call to AddProp or SetIt<typeparamref name="T"/>, will cause an exception to be thrown.
        /// </summary>
        private bool OnlyTypedAccess;

        /// <summary>
        /// Used to create Delegates when the type of the value is not known at run time.
        /// </summary>
        static private Type GMT_TYPE = typeof(GenericMethodTemplates);

        #endregion

        #region Constructor

        public PropBagBase(byte dummy) { } // This is called when reflection is used to discover the methods and properties.

        public PropBagBase() : this(PropBagTypeSafetyMode.AllPropsMustBeRegistered, null) { }
        //public PropBagBase() : this(PropBagTypeSafetyMode.OnlyTypedAccess, null) { }

        public PropBagBase(PropBagTypeSafetyMode typeSafetyMode) : this(typeSafetyMode, null) { }

        public PropBagBase(PropBagTypeSafetyMode typeSafetyMode, AbstractPropFactory thePropFactory) 
        {
            this.TypeSafetyMode = typeSafetyMode;
            switch (typeSafetyMode)
            {
                case PropBagTypeSafetyMode.AllPropsMustBeRegistered:
                    {
                        AllPropsMustBeRegistered = true;
                        OnlyTypedAccess = false;
                        break;
                    }
                case PropBagTypeSafetyMode.OnlyTypedAccess:
                    {
                        AllPropsMustBeRegistered = false;
                        OnlyTypedAccess = true;
                        break;
                    }
                case PropBagTypeSafetyMode.Loose:
                    {
                        AllPropsMustBeRegistered = false;
                        OnlyTypedAccess = false;
                        break;
                    }
                default:
                    throw new ApplicationException("Unexpected value for typeSafetyMode parameter.");
            }

            // Use the "built-in" property factory, if the caller did not supply one.
            this.ThePropFactory = thePropFactory ?? new PropFactory();

            tVals = new Dictionary<string, IPropGen>();
        }

        public PropBagBase(ControlModel.PropModel pm) : this(pm.TypeSafetyMode, null)
        {
            foreach (DRM.PropBag.ControlModel.PropItem pi in pm.Props)
            {
                object ei = pi.ExtraInfo;

                Delegate comparer;
                bool useRefEquality;

                if (pi.ComparerField == null)
                {
                    comparer = null;
                    useRefEquality = false;
                }
                else
                {
                    comparer = pi.ComparerField.Comparer;
                    useRefEquality = pi.ComparerField.UseRefEquality;
                }

                if (pi.InitialValueField == null)
                {
                    pi.InitialValueField = PropInitialValueField.UndefinedInitialValueField;
                }

                IPropGen pg;

                if (pi.HasStore && !pi.InitialValueField.SetToUndefined)
                {
                    bool useDefault = pi.InitialValueField.SetToDefault;
                    string value;


                    if (pi.InitialValueField.SetToEmptyString && pi.PropertyType == typeof(Guid))
                    {
                        const string EMPTY_GUID = "00000000-0000-0000-0000-000000000000";
                        value = EMPTY_GUID;
                    }
                    else
                    {
                        value = pi.InitialValueField.GetStringValue();
                    }

                    pg = ThePropFactory.CreateGenFromString(pi.PropertyType, value, useDefault, pi.PropertyName, ei, pi.HasStore, pi.TypeIsSolid,
                        pi.DoWhenChangedField.DoWhenChangedAction, pi.DoWhenChangedField.DoAfterNotify, comparer, useRefEquality);
                }
                else
                {
                    pg = ThePropFactory.CreateGenWithNoValue(pi.PropertyType, pi.PropertyName, ei, pi.HasStore, pi.TypeIsSolid,
                        pi.DoWhenChangedField.DoWhenChangedAction, pi.DoWhenChangedField.DoAfterNotify, comparer, useRefEquality);
                }
                AddProp(pi.PropertyName, pg);
            }
        }

        private object GetValue(ControlModel.PropInitialValueField ivf)
        {
            //Debug.Assert(ivf.SetToDefault == false, "Set To Default is true on call to GetValue.");
            Debug.Assert(ivf.SetToUndefined == false, "Set To Undefined is true on call to GetValue.");

            if (ivf.SetToNull) return null;
            if (ivf.SetToEmptyString) return string.Empty;

            return ivf.InitialValue;
         }

        protected void ClearEventSubscribers()
        {
            foreach (var x in tVals.Values)
            {
                x.CleanUp();
            }
        }

        protected void ClearAll()
        {
            // TODO: Fix Me.
            //DelegateCacheProvider.DoSetDelegateCache.Clear();
            //doSetDelegateDict.Clear();
            ClearEventSubscribers();
            tVals.Clear();
        }

        protected ITypeSafePropBag GetAccessWrapper()
        {

                Dictionary<string, Type> typeDefs =
                    tVals
                    .Select(x => new KeyValuePair<string, Type>(x.Key, x.Value.Type))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);

                

            return null;
        }

        #endregion

        #region Property Access Methods

        protected object this[string propertyName]
        {
            get
            {
                return GetIt(propertyName);
            }
            set
            {
                SetItWithType(value, null, propertyName);
            }
        }

        protected object this[string typeName, string propertyName]
        {
            get
            {
                return GetIt(propertyName, Type.GetType(propertyName));
            }
            set
            {
                Type type = Type.GetType(typeName);
                SetItWithType(value, type, propertyName);
            }
        }

        public object GetItWithNoType([CallerMemberName] string propertyName = null)
        {
            return GetIt(propertyName, null);
        }


        public object GetIt([CallerMemberName] string propertyName = null, Type propertyType = null)
        {
            if (propertyType == null && OnlyTypedAccess)
            {
                throw new InvalidOperationException("Attempt to access property using this method is not allowed when TypeSafetyMode is 'OnlyTypedAccess.'");
            }

            // This will throw an exception if no value has been added to the _tVals dictionary with a key of propertyName,
            // either by calling AddProp or SetIt.
            IPropGen genProp = GetGenProp(propertyName, ThePropFactory.ProvidesStorage);

            if(propertyType != null)
            {
                if (!genProp.TypeIsSolid)
                {
                    MakeTypeSolid(ref genProp, propertyType, propertyName);
                    tVals[propertyName] = genProp;
                }
                else
                {
                    if (propertyType != genProp.Type)
                    {
                        throw new InvalidOperationException(string.Format("Attempting to get property: {0} whose type is {1}, with a call whose type parameter is {2} is invalid.", propertyName, genProp.Type.ToString(), propertyType.ToString()));
                    }
                }
            }

            // This uses reflection.
            return genProp.Value;
        }

        public T GetIt<T>([CallerMemberName] string propertyName = null)
        {
            return (T) GetIProp<T>(propertyName).Value;
        }

        public IProp<T> GetIProp<T>([CallerMemberName] string propertyName = null)
        {
            bool hasStore = ThePropFactory.ProvidesStorage;
            IPropGen genProp = GetGenProp(propertyName, hasStore);
            return CheckTypeInfo<T>(genProp, propertyName, tVals);
        }

        public bool SetItWithNoType(object value, [CallerMemberName]string propertyName = null)
        {
            return SetItWithType(value, null, propertyName);
        }



        /// <summary>
        /// Set's the value of the property with optional type information.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="propertyType">If unknown, set this parameter to null.</param>
        /// <param name="propertyName"></param>
        public bool SetItWithType(object value, Type propertyType, [CallerMemberName]string propertyName = null)
        {
            if (propertyType == null && OnlyTypedAccess)
            {
                throw new InvalidOperationException("Attempt to access property using this method is not allowed when TypeSafetyMode is 'OnlyTypedAccess.'");
            }

            IPropGen genProp;
            try
            {
                genProp = GetGenProp(propertyName, ThePropFactory.ProvidesStorage);
            }
            catch (InvalidOperationException)
            {
                if (AllPropsMustBeRegistered)
                    throw;

                if (propertyType == null)
                    genProp = ThePropFactory.CreatePropInferType(value, propertyName, null, true);
                else
                {
                    bool typeIsSolid = !(propertyType == typeof(object));
                    genProp = ThePropFactory.CreateGenFromObject(propertyType, value, propertyName, null, true, typeIsSolid, null, false, null);
                }

                AddProp(propertyName, genProp);

                // No point in calling DoSet, it would find that the value is the same and do nothing.
                return true;
            }

            if (value != null)
            {
                if (!genProp.TypeIsSolid)
                {
                    try
                    {
                        Type newType;
                        if (propertyType != null)
                        {
                            // Use the type provided by the caller.
                            newType = propertyType;
                        }
                        else
                        {
                            // TODO, we probably need to be more creative when determining the type of this new value.
                            newType = value.GetType();
                        }

                        MakeTypeSolid(ref genProp, newType, propertyName);
                        tVals[propertyName] = genProp;
                    }
                    catch (InvalidCastException ice)
                    {
                        throw new ApplicationException(string.Format("The property: {0} was originally set to null, now its being set to a value whose type is a value type; value types don't allow setting to null.", propertyName), ice);
                    }
                }
                else
                {
                    Type newType;
                    if (propertyType != null)
                    {
                        // Use the type provided by the caller.
                        newType = propertyType;
                    }
                    else
                    {
                        // Object.GetType() is sufficent here, since AreTypesSame will handle comparison nuances.
                        newType = value.GetType();
                    }

                    if (!AreTypesSame(newType, genProp.Type))
                    {
                        throw new ApplicationException(string.Format("Attempting to set property: {0} whose type is {1}, with a call whose type parameter is {2} is invalid.", propertyName, genProp.Type.ToString(), newType.ToString()));
                    }
                }
            }
            else
            {
                if (genProp.TypeIsSolid && genProp.Type.IsValueType)
                {
                    throw new InvalidOperationException(string.Format("Cannot set property: {0} to null, it is a value type.", propertyName));
                }
            }

            // This uses reflection on first access.
            //DoSetDelegate setPropDel = GetPropSetterDelegate(genProp);
            DoSetDelegate setPropDel = DelegateCacheProvider.DoSetDelegateCache.GetOrAdd(genProp.Type);
            return setPropDel(value, this, propertyName, genProp);
        }

        public bool SetIt<T>(T value, [CallerMemberName]string propertyName = null)
        {
            IPropGen genProp;

            try
            {
                genProp = GetGenProp(propertyName, ThePropFactory.ProvidesStorage);
            }
            catch (InvalidOperationException)
            {
                if (AllPropsMustBeRegistered)
                {
                    throw new InvalidOperationException(string.Format("Property: {0} has not been defined with a call to AddProp() and the operation setting 'AllPropsMustBeRegistered' is set to true.", propertyName));
                }

                // Property has not been defined yet, let's create a definition for it now and initialize the value.
                genProp = ThePropFactory.Create<T>(value, propertyName);
                AddProp(propertyName, genProp);

                // No reason to call DoSet, it will find no change and do nothing.
                return true;
            }

            IProp<T> prop = CheckTypeInfo<T>(genProp, propertyName, tVals);

            return DoSet(value, propertyName, prop);
        }

        /// <summary>
        /// For use when the Property Bag's internal storage is not appropriate. This allows
        /// the property implementor to use a backing store of their choice.
        /// The property must be registered with a call to AddPropNoStore.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="newValue">The new value to use to update the property. No operation will be performed if this value is the same as the current value.</param>
        /// <param name="curValue">The current value of the property, must be specified using the ref keyword.</param>
        /// <param name="propertyName"></param>
        /// <returns>True if the value was updated, otherwise false.</returns>
        public bool SetIt<T>(T newValue, ref T curValue, [CallerMemberName]string propertyName = null)
        {
            IPropGen genProp;

            try
            {
                // This will fail if the property has a backing store.
                genProp = GetGenProp(propertyName, desiredHasStoreValue: false);
            }
            catch (InvalidOperationException)
            {
                if (AllPropsMustBeRegistered)
                {
                    throw new InvalidOperationException(string.Format("Property: {0} has not been defined with a call to AddProp() and the operation setting 'AllPropsMustBeRegistered' is set to true.", propertyName));
                }

                // Property has not been defined yet, let's create a definition for it now 
                genProp = ThePropFactory.CreateWithNoValue<T>(propertyName, hasStorage: false, typeIsSolid: true);
                AddProp(propertyName, genProp);
            }

            IProp<T> prop = CheckTypeInfo<T>(genProp, propertyName, tVals);

            bool theSame = prop.Compare(newValue, curValue);

            if (!theSame)
            {
                // Save the value before the update.
                T oldValue = curValue;

                OnPropertyChanging(propertyName);

                // Make the update.
                curValue = newValue;

                // Raise notify events.
                DoNotifyWork<T>(oldValue, newValue, propertyName, prop);
            }

            // Return true, if the new value was found to be different than the current value.
            return !theSame;
        }

        #endregion

        #region Subscribe to Property Changed Event Helpers

        // This is used to allow the caller to get notified only when a particular property is changed with values.
        // It can be used in any of the three modes, but is especially handy for Loose mode.
        public void SubscribeToPropChanged(Action<object, object> doOnChange, string propertyName)
        {
            IPropGen genProp;
            try
            {
                genProp = GetGenProp(propertyName, ThePropFactory.ProvidesStorage);
            }
            catch (InvalidOperationException)
            {
                if (AllPropsMustBeRegistered)
                    throw new InvalidOperationException(string.Format("Property: {0} has not been declared by calling AddProp, nor has its value been set by calling SetIt<T>. Cannot use this method in this case. Declare by calling AddProp, or use the SetIt<T> method.", propertyName));

                // TODO: Make sure it makes sense to allow this operation when OnlyTypedAccess is true.
                //if (OnlyTypedAccess)
                //{
                //    throw new InvalidOperationException(string.Format("Property: {0} has not been defined with a call to AddProp or any SetIt<T> call and the TypeSafety mode is set to 'OnlyTypeAccesss.'", propertyName));
                //}

                genProp = ThePropFactory.CreateWithNoValue<object>(propertyName, null, true, false, null, false, null);
                AddProp(propertyName, genProp);
            }

            genProp.SubscribeToPropChanged(doOnChange);

        }

        public bool UnSubscribeToPropChanged(Action<object, object> doOnChange, string propertyName)
        {
            IPropGen genProp;
            try
            {
                genProp = GetGenProp(propertyName, ThePropFactory.ProvidesStorage);
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            return genProp.UnSubscribeToPropChanged(doOnChange);
        }

        // Allow callers to easily subscribe to PropertyChangedWithTVals.
        public void SubscribeToPropChanged<T>(Action<T, T> doOnChange, string propertyName)
        {
            
            //IProp<T> prop = GetPropDef<T>(propertyName);

            IProp<T> prop = GetIProp<T>(propertyName);

            prop.SubscribeToPropChanged(doOnChange);
        }

        public bool UnSubscribeToPropChanged<T>(Action<T, T> doOnChange, string propertyName)
        {
            IPropGen genProp;
            try
            {
                genProp = GetGenProp(propertyName, ThePropFactory.ProvidesStorage);

            }
            catch (InvalidOperationException)
            {
                return false;
            }

            IProp<T> prop = (IProp<T>)genProp;

            return prop.UnSubscribeToPropChanged(doOnChange);
        }

        public void SubscribeToPropChanged<T>(PropertyChangedWithTValsHandler<T> eventHandler, string propertyName)
        {
            // This will create the property if it does not exist with an undefined initial value.
            //IProp<T> prop = GetPropDef<T>(propertyName);

            // This wll throw an InvalidOperationException if the property does not exist.
            IProp<T> prop = GetIProp<T>(propertyName);

            prop.PropertyChangedWithTVals += eventHandler;
        }

        public void UnSubscribeToPropChanged<T>(PropertyChangedWithTValsHandler<T> eventHandler, string propertyName)
        {
            //IPropGen genProp = GetGenProp(propertyName);

            //IProp<T> prop = CheckTypeInfo<T>(genProp, propertyName, tVals);

            IProp<T> prop = GetIProp<T>(propertyName);

            prop.PropertyChangedWithTVals -= eventHandler;
        }

        /// <summary>
        /// Uses the name of the property or event accessor of the calling method to indentify the property,
        /// if the propertyName argument is not specifed.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="eventHandler"></param>
        /// <param name="eventPropertyName"></param>
        protected void AddToPropChanged<T>(PropertyChangedWithTValsHandler<T> eventHandler, [CallerMemberName] string eventPropertyName = null)
        {
            string propertyName = GetPropNameFromEventProp(eventPropertyName);
            SubscribeToPropChanged<T>(eventHandler, propertyName);
        }

        /// <summary>
        /// Uses the name of the property or event accessor of the calling method to indentify the property,
        /// if the propertyName argument is not specifed.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="eventHandler"></param>
        /// <param name="eventPropertyName"></param>
        protected void RemoveFromPropChanged<T>(PropertyChangedWithTValsHandler<T> eventHandler, [CallerMemberName] string eventPropertyName = null)
        {
            string propertyName = GetPropNameFromEventProp(eventPropertyName);
            UnSubscribeToPropChanged<T>(eventHandler, propertyName);
        }

        #endregion

        #region Public Methods

        public bool PropertyExists([CallerMemberName] string propertyName = null)
        {
            return tVals.ContainsKey(propertyName);
        }

        public System.Type GetTypeOfProperty([CallerMemberName] string propertyName = null)
        {
            return GetGenProp(propertyName, ThePropFactory.ProvidesStorage).Type;
        }

        #endregion

        #region Property Management

        protected void AddProp<T>(string propertyName, IProp<T> prop)
        {
            IPropGen pg = (IPropGen)prop;
            tVals.Add(propertyName, pg);
        }

        protected void AddProp(string propertyName, IPropGen prop)
        {
            tVals.Add(propertyName, prop);
        }

        protected void RemoveProp(string propertyName)
        {
            tVals.Remove(propertyName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="doWhenChanged"></param>
        /// <param name="doAfterNotify"></param>
        /// <param name="propertyName"></param>
        /// <returns>True, if there was an existing Action in place for this property.</returns>
        protected bool RegisterDoWhenChanged<T>(Action<T, T> doWhenChanged, bool doAfterNotify, string propertyName)
        {
            IProp<T> prop = GetIProp<T>(propertyName);
            //IProp<T> prop = GetPropDef<T>(propertyName);
            return prop.UpdateDoWhenChangedAction(doWhenChanged, doAfterNotify);
        }

        /// <summary>
        /// Makes a copy of the core list.
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, ValPlusType> GetAllPropNamesAndTypes()
        {
            IEnumerable<KeyValuePair<string, ValPlusType>> list =
                tVals.Select(x => new KeyValuePair<string, ValPlusType>(x.Key, x.Value.ValuePlusType())).ToList();

            IDictionary<string, ValPlusType> result = list.ToDictionary(pair => pair.Key, pair => pair.Value);
            return result; 
        }



        /// <summary>
        /// Returns all of the values in dictionary of objects, keyed by PropertyName.
        /// </summary>
        public IDictionary<string, object> GetAllPropertyValues()
        {
            // This uses reflection.
            Dictionary<string, object> result = new Dictionary<string, object>();

            foreach (KeyValuePair<string, IPropGen> kvp in tVals)
            {
                result.Add(kvp.Key, kvp.Value.Value);
            }
            return result;
        }

        public IList<string> GetAllPropertyNames()
        {
            List<string> result = new List<string>();

            foreach (var y in tVals.Keys)
            {
                result.Add(y);
            }
            return result;
        }

        #endregion

        #region Private Methods and Properties

        private bool DoSet<T>(T newValue, string propertyName, IProp<T> prop)
        {
            if (!prop.ValueIsDefined)
            {
                // Update and only raise the standard OnPropertyChanged
                // Since there's no way to pass an undefined value to the other OnPropertyChanged event subscribers.
                prop.TypedValue = newValue;

                // Raise the standard PropertyChanged event
                OnPropertyChanged(propertyName);
                return true; // If it was originally unasigned, then it will always be updated.
            }
            else
            {
                bool theSame = prop.CompareTo(newValue);

                if (!theSame)
                {
                    // Save the value before the update.
                    T oldValue = prop.TypedValue;

                    OnPropertyChanging(propertyName);

                    // Make the update.
                    prop.TypedValue = newValue;

                    // Raise notify events.
                    DoNotifyWork(oldValue, newValue, propertyName, prop);
                }
                return !theSame;
            }
        }

        private void DoNotifyWork<T>(T oldVal, T newValue, string propertyName, IProp<T> prop)
        {
            if (prop.DoAfterNotify)
            {
                // Raise the standard PropertyChanged event
                OnPropertyChanged(propertyName);

                // The typed, PropertyChanged event defined on the individual property.
                prop.OnPropertyChangedWithTVals(propertyName, oldVal, newValue);

                // The un-typed, PropertyChanged event defined on the individual property.
                prop.OnPropertyChangedWithVals(propertyName, oldVal, newValue);

                // The un-typed, PropertyChanged shared event.
                OnPropertyChangedWithVals(propertyName, oldVal, newValue);

                // then perform the call back.
                prop.DoWhenChanged(oldVal, newValue);
            }
            else
            {
                // Peform the call back,
                prop.DoWhenChanged(oldVal, newValue);

                // Raise the standard PropertyChanged event
                OnPropertyChanged(propertyName);

                // The typed, PropertyChanged event defined on the individual property.
                prop.OnPropertyChangedWithTVals(propertyName, oldVal, newValue);

                // The un-typed, PropertyChanged event defined on the individual property.
                prop.OnPropertyChangedWithVals(propertyName, oldVal, newValue);

                // The un-typed, PropertyChanged shared event.
                OnPropertyChangedWithVals(propertyName, oldVal, newValue);
            }
        }

        protected IPropGen GetGenProp(string propertyName, bool? desiredHasStoreValue = true)
        {
            if (propertyName == null) throw new ArgumentNullException("propertyName", "PropertyName is null on call to GetValue.");

            IPropGen genProp;
            try
            {
                genProp = tVals[propertyName];
            }
            catch (KeyNotFoundException)
            {
                if (TypeSafetyMode == PropBagTypeSafetyMode.AllPropsMustBeRegistered)
                {
                    throw new InvalidOperationException(string.Format("Property: {0} has not been declared by calling AddProp. Cannot use this method in this case. Declare by calling AddProp.", propertyName));
                }
                else
                {
                    throw new InvalidOperationException(string.Format("No property: {0} exists in this PropBag.", propertyName));
                }
            }

            if (desiredHasStoreValue.HasValue && desiredHasStoreValue.Value != genProp.HasStore)
            {
                if (desiredHasStoreValue.Value)
                    //Caller needs property to have a backing store.
                    throw new InvalidOperationException(string.Format("Property: {0} has no backing store held by this instance of PropBag. This operation can only be performed on properties for which a backing store is held by this instance.", propertyName));
                else
                    throw new InvalidOperationException(string.Format("Property: {0} has a backing store held by this instance of PropBag. This operation can only be performed on properties for which no backing store is kept by PropBag.", propertyName));
            }

            return genProp;
        }

        private IProp<T> CheckTypeInfo<T>(IPropGen genProp, string propertyName, IDictionary<string, IPropGen> dict)
        {
            if (!genProp.TypeIsSolid)
            {
                try
                {
                    MakeTypeSolid(ref genProp, typeof(T), propertyName);
                    dict[propertyName] = genProp;
                }
                catch (InvalidCastException ice)
                {
                    throw new ApplicationException(string.Format("The property: {0} was originally set to null, now its being set to a value whose type is a value type; value types don't allow setting to null.", propertyName), ice);
                }
            }
            else
            {
                if (!AreTypesSame(typeof(T), genProp.Type))
                {
                    throw new ApplicationException(string.Format("Attempting to set property: {0} whose type is {1}, with a call whose type parameter is {2} is invalid.", propertyName, genProp.Type.ToString(), typeof(T).ToString()));
                }
            }

            return (IProp<T>)genProp;
        }

        // TODO: These next three methods may be handy in the future -- or should we delete them.
        // See GetIProp<T>(string propertyName)

        //private IPropGen GetTypeCheckedGenProp<T>(string propertyName)
        //{
        //    IPropGen genProp;
        //    GetPropDef<T>(propertyName, out genProp);
        //    return genProp;
        //}

        //private IProp<T> GetPropDef<T>(string propertyName)
        //{
        //    IPropGen genProp;
        //    return GetPropDef<T>(propertyName, out genProp);
        //}

        ///// <summary>
        ///// This should only be called from methods that do not include a new or initial value for the property.
        ///// </summary>
        ///// <typeparam name="T"></typeparam>
        ///// <param name="propertyName"></param>
        ///// <param name="genProp"></param>
        ///// <returns></returns>
        //private IProp<T> GetPropDef<T>(string propertyName, out IPropGen genProp)
        //{
        //    try
        //    {
        //        genProp = GetGenProp(propertyName);
        //    }
        //    catch (KeyNotFoundException)
        //    {
        //        if (AllPropsMustBeRegistered)
        //        {
        //            throw new InvalidOperationException(string.Format("Property: {0} has not been defined with a call to AddProp() and the operation setting 'AllPropsMustBeRegistered' is set to true.", propertyName));
        //        }

        //        // Property has not been defined yet, let's create a definition for it now and initialize the value.
        //        genProp = ThePropFactory.CreateWithNoValue<T>(propertyName);
        //        AddProp(propertyName, genProp);
        //    }

        //    IProp<T> prop = CheckTypeInfo<T>(genProp, propertyName, tVals);

        //    return prop;
        //}

        private void MakeTypeSolid(ref IPropGen genProp, Type newType, string propertyName)
        {
            Type currentType = genProp.Type;

            Debug.Assert(genProp.Value == null, "The current value of the property should be null when MakeTypeSolid is called.");

            System.Type underlyingtype = Nullable.GetUnderlyingType(newType);

            // Check to see if the new type is a non-nullable, value type.
            // If it is, then it was invalid to set this property to null in the first place.
            // Consider setting value to the type's default value instead of throwing this exception.
            if (underlyingtype == null && newType.IsValueType)
                throw new InvalidCastException("The new type is a non-nullable value type.");

            // We are using strict equality here, since we have the oportunity to update the type to anything
            // that is assignable from a value of type object (which is everything.)
            if (newType != currentType)
            {
                // Next statement uses reflection.
                object curValue = genProp.Value;

                IPropGen newwGenProp = ThePropFactory.CreateGenFromObject(newType, curValue, propertyName, null, true, true, null, false, null, false);

                //genProp.UpdateWithSolidType(newType, curValue);
                genProp = newwGenProp;
            }
        }

        // Its important to make sure new is new and cur is cur.
        private bool AreTypesSame(Type newType, Type curType)
        {
            if (newType == curType)
                return true;

            Type aUnder = Nullable.GetUnderlyingType(newType);

            if (aUnder != null)
            {
                // Compare the underlying type of this new value and the target property's underlying type.
                Type bUnder = Nullable.GetUnderlyingType(curType);

                return aUnder == bUnder || aUnder.UnderlyingSystemType == bUnder.UnderlyingSystemType;
            }
            else if(newType.IsGenericType)
            {
                return curType.IsAssignableFrom(newType);
            }
            else
            {
                return newType.UnderlyingSystemType == curType.UnderlyingSystemType;
            }

        }

        /// <summary>
        /// Given a string in the form "{0}Changed", where {0} is the underlying property name, parse out and return the value of {0}.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        protected string GetPropNameFromEventProp(string x)
        {
            //PropStringChanged
            return x.Substring(0, x.Length - 7);
        }

        public object GetValueGen(object host, string propertyName, Type propertyType)
        {
            return ((IPropBag)host).GetIt(propertyName, propertyType);
        }

        public void SetValueGen(object host, string propertyName, Type propertyType, object value)
        {
            ((IPropBag)host).SetItWithType(value, propertyType, propertyName);
        }

        #endregion

        #region Methods to Raise Events

        // Raise Standard Events
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = Interlocked.CompareExchange(ref PropertyChanged, null, null);

            if (handler != null)
            {
                // TOOD:! Fix This!!!
                propertyName = "Item[]";
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        protected void OnPropertyChanging(string propertyName)
        {
            PropertyChangingEventHandler handler = Interlocked.CompareExchange(ref PropertyChanging, null, null);

            if (handler != null)
            {
                // TOOD:! Fix This!!!
                propertyName = "Item[]";
                handler(this, new PropertyChangingEventArgs(propertyName));
            }
        }

        protected void OnPropertyChangedWithVals(string propertyName, object oldVal, object newVal)
        {
            PropertyChangedWithValsHandler handler = Interlocked.CompareExchange(ref PropertyChangedWithVals, null, null);

            if (handler != null)
                handler(this, new PropertyChangedWithValsEventArgs(propertyName, oldVal, newVal));
        }

        #endregion

        #region Generic Method Support

        //private DoSetDelegate GetPropSetterDelegate(IPropGen genProp)
        //{
        //    return DelegateCacheProvider.DoSetDelegateCache.GetOrAdd(genProp.Type);
        //}

        // Method Templates for Property Bag
        internal static class GenericMethodTemplates
        {
            static Lazy<MethodInfo> theSingleGenericDoSetBridgeMethodInfo;

            public static MethodInfo GenericDoSetBridgeMethodInfo { get { return theSingleGenericDoSetBridgeMethodInfo.Value; } }

            static GenericMethodTemplates()
            {
                theSingleGenericDoSetBridgeMethodInfo = new Lazy<MethodInfo>(() =>
                    GMT_TYPE.GetMethod("DoSetBridge", BindingFlags.Static | BindingFlags.NonPublic), 
                    LazyThreadSafetyMode.PublicationOnly);
            }

            private static bool DoSetBridge<T>(object value, PropBagBase target, string propertyName, object prop)
            {
                return target.DoSet<T>((T)value, propertyName, (IProp<T>)prop);
            }

            public static DoSetDelegate GetDoSetDelegate(Type typeOfThisValue)
            {
                MethodInfo methInfoSetProp = GenericMethodTemplates.GenericDoSetBridgeMethodInfo.MakeGenericMethod(typeOfThisValue);
                DoSetDelegate result = (DoSetDelegate)Delegate.CreateDelegate(typeof(DoSetDelegate), methInfoSetProp);

                return result;
            }
        }

        #endregion

    }
}
