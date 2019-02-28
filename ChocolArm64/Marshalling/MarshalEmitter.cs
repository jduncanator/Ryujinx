using ChocolArm64.Memory;
using ChocolArm64.State;
using ChocolArm64.Translation;
using System;
using System.Reflection;
using System.Reflection.Emit;

namespace ChocolArm64.Marshalling
{
    internal class MarshalEmitter
    {
        private const string ThunkMethodNamePrefix = "_ryujinx_thunk_";

        private Type DelegateType { get; set; }

        private MethodInfo MethodInfo { get; set; }

        public MarshalEmitter(Type delegateType)
        {
            if(!IsDelegate(delegateType))
            {
                throw new ArgumentException("'delegateType' is not a delegate.", "delegateType");
            }

            this.DelegateType = delegateType;
            this.MethodInfo = delegateType.GetMethod("Invoke");
        }

        public Delegate Emit()
        {
            var dynamicMethod = new DynamicMethod(
                ThunkMethodNamePrefix + DelegateType.Name,
                GetDelegateReturnType(typeof(ArmSubroutine)),
                GetDelegateParameterTypes(typeof(ArmSubroutine))
            );

            EmitInstructions(dynamicMethod.GetILGenerator());

            return dynamicMethod.CreateDelegate(typeof(ArmSubroutine));
        }

        private void EmitInstructions(ILGenerator generator)
        {

        }

        public static MarshalEmitter Create<T>()
            where T : Delegate
        {
            return new MarshalEmitter(typeof(T));
        }

        private static Type GetDelegateReturnType(Type type)
        {
            var invoke = type.GetMethod("Invoke");

            if (invoke == null)
            {
                throw new ArgumentException("'type' is not a delegate.", "type");
            }

            return invoke.ReturnType;
        }

        private static Type[] GetDelegateParameterTypes(Type type)
        {
            var invoke = type.GetMethod("Invoke");

            if (invoke == null)
            {
                throw new ArgumentException("'type' is not a delegate.", "type");
            }

            var parameters = invoke.GetParameters();
            var typeParameters = new Type[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                typeParameters[i] = parameters[i].ParameterType;
            }

            return typeParameters;
        }


        private static bool IsDelegate(Type type)
        {
            return typeof(MulticastDelegate).IsAssignableFrom(type.BaseType);
        }
    }
}
