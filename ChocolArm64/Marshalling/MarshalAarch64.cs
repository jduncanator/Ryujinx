using ChocolArm64.Translation;
using System;
using System.Reflection;

namespace ChocolArm64.Marshalling
{
    internal static class MarshalAarch64
    {
        public static ArmSubroutine CreateMarshalThunk<T>(T method)
            where T : Delegate
        {
            ValidateMethodSignature<T>();

            var emitter = MarshalEmitter.Create<T>();
            var del = emitter.Emit();

            return (ArmSubroutine)del;
        }
        
        public static T CreateUnmarshalThunk<T>(ArmSubroutine subroutine)
            where T : Delegate
        {
            ValidateMethodSignature<T>();
         
            // Emit validation to verify we are executing in a CpuThread/KProcess
            

            return null;
        }

        private static void ValidateMethodSignature<T>()
            where T : Delegate
        {
            var methodInfo = GetDelegateMethodInfo<T>();

            ValidateParameterType(methodInfo.ReturnParameter);

            foreach(var parm in methodInfo.GetParameters())
            {
                ValidateParameterType(parm);
            }
        }

        private static void ValidateParameterType(ParameterInfo paramInfo)
        {
            var type = paramInfo.ParameterType;

            if(!type.IsValueType)
            {
                throw new ArgumentException(String.Format(
                    "Reference types not supported in {0}.", paramInfo.IsRetval ? "return value" : paramInfo.Name
                ));
            }
        }

        private static MethodInfo GetDelegateMethodInfo<T>()
            where T : Delegate
        {
            return typeof(T).GetMethod("Invoke");
        }
    }
}
