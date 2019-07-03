﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace FastDeepCloner
{
#if NETSTANDARD2_0 || NETCOREAPP2_0 || NET451
    internal static class ConvertToInterface
    {

        private static AssemblyBuilder _ab;
        private static ModuleBuilder _mb;
        public static Type Convert(Type interfaceType, Type type)
        {
            try
            {
                if (!interfaceType.IsInterface)
                    throw new Exception("Type need to be an interface");
                var className = interfaceType.Name + "__" + (type.IsAnonymousType() ? Guid.NewGuid().ToString("N") : type.Name) + "__FastDeepClonerConvertToInterface";
                if (_ab == null)
                {
                    var assmName = new AssemblyName("FastDeepCloner.DynamicAssembly.Interface");
                    _ab = AssemblyBuilder.DefineDynamicAssembly(assmName, AssemblyBuilderAccess.Run);
                    _mb = _ab.DefineDynamicModule(assmName.Name);
                }

                TypeBuilder typeBuilder = _mb.DefineType(className, TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit, typeof(object));
                typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);

                var props = DeepCloner.GetFastDeepClonerProperties(interfaceType).FindAll(x => x.ReadAble);

                CreateProperty(props, typeBuilder, interfaceType);

                if (!interfaceType.IsAssignableFrom(type))
                    typeBuilder.AddInterfaceImplementation(interfaceType);
                else return type;
                return typeBuilder.CreateTypeInfo().AsType();
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static ConstructorBuilder CreateConstructor(List<IFastDeepClonerProperty> props, TypeBuilder typeBuilder)
        {
            var types = props.Select(x => x.PropertyType);
            return typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, types.ToArray());
        }


        public static void CreateProperty(List<IFastDeepClonerProperty> props, TypeBuilder typeBuilder, Type interfaceType)
        {

            var lstField = new List<FieldBuilder>();
            foreach (var property in props)
            {
                var name = property.Name;
                string field = "_" + name.ToLower();
                FieldBuilder fieldBldr = typeBuilder.DefineField(field, property.PropertyType, FieldAttributes.Private);
                PropertyBuilder propBldr = typeBuilder.DefineProperty(name, PropertyAttributes.HasDefault, property.PropertyType, null);
                MethodAttributes getSetAttr = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig;

                MethodBuilder getPropBldr = typeBuilder.DefineMethod("get_" + name, getSetAttr, property.PropertyType, Type.EmptyTypes);

                ILGenerator getIL = getPropBldr.GetILGenerator();
                getIL.Emit(OpCodes.Ldarg_0);
                getIL.Emit(OpCodes.Ldfld, fieldBldr);
                getIL.Emit(OpCodes.Ret);

                MethodBuilder setPropBldr = typeBuilder.DefineMethod("set_" + name, getSetAttr, null, new Type[] { property.PropertyType });

                ILGenerator setIL = setPropBldr.GetILGenerator();

                setIL.Emit(OpCodes.Ldarg_0);
                setIL.Emit(OpCodes.Ldarg_1);
                setIL.Emit(OpCodes.Stfld, fieldBldr);
                setIL.Emit(OpCodes.Ret);

                propBldr.SetGetMethod(getPropBldr);
                propBldr.SetSetMethod(setPropBldr);
                lstField.Add(fieldBldr);

            }

            var constructor = CreateConstructor(props, typeBuilder).GetILGenerator();
            foreach (var p in props)
            {
                var index = props.IndexOf(p);
                constructor.Emit(OpCodes.Ldarg_0);
                constructor.Emit(OpCodes.Ldarg_S, index + 1);
                constructor.Emit(OpCodes.Stfld, lstField[index]);
            }
            constructor.Emit(OpCodes.Ret); // finish the constructor
        }
    }
#endif
}