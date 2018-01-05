using System;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using Microsoft.VisualStudio.DesignTools.ImageSet;

namespace VisualAssetGenerator.Extensions
{
    public class DelegateWrapper
    {
        public static T WrapAs<T>(Delegate impl) where T : class
        {
            var generator = new ProxyGenerator();
            var proxy = generator.CreateInterfaceProxyWithoutTarget(typeof(T), new MethodInterceptor(impl));
            return (T)proxy;
        }

        public static TInterface WrapAs<TInterface>(Delegate d1, Delegate d2)
        {
            var generator = new ProxyGenerator();
            var options = new ProxyGenerationOptions { Selector = new DelegateSelector() };
            var proxy = generator.CreateInterfaceProxyWithoutTarget(
                typeof(TInterface),
                new Type[0],
                options,
                new MethodInterceptor(d1),
                new MethodInterceptor(d2));
            return (TInterface)proxy;
        }

        public static object WrapAs(Delegate d1, Delegate d2, Type interfaceType)
        {
            var generator = new ProxyGenerator();
            var options = new ProxyGenerationOptions { Selector = new DelegateSelector() };
            var proxy = generator.CreateInterfaceProxyWithoutTarget(
                interfaceType,
                new Type[0],
                options,
                new MethodInterceptor(d1),
                new MethodInterceptor(d2));
            return proxy;
        }

        internal class MethodInterceptor : IInterceptor
        {
            public MethodInterceptor(Delegate @delegate)
            {
                Delegate = @delegate;
            }

            public Delegate Delegate { get; }

            public void Intercept(IInvocation invocation)
            {
                var result = Delegate.DynamicInvoke(invocation.Arguments);
                invocation.ReturnValue = result;
            }
        }

        internal class DelegateSelector : IInterceptorSelector
        {
            public IInterceptor[] SelectInterceptors(Type type, MethodInfo method, IInterceptor[] interceptors)
            {
                foreach (var interceptor in interceptors)
                {
                    var methodInterceptor = interceptor as MethodInterceptor;
                    if (methodInterceptor == null)
                        continue;
                    var d = methodInterceptor.Delegate;
                    if (IsEquivalent(d, method))
                        return new[] { interceptor };
                }
                throw new ArgumentException();
            }

            private static bool IsEquivalent(Delegate d, MethodInfo method)
            {
                var dm = d.Method;
                if (!method.ReturnType.IsAssignableFrom(dm.ReturnType))
                    return false;
                var parameters = method.GetParameters();
                var dp = dm.GetParameters();
                if (parameters.Length != dp.Length)
                    return false;
                for (int i = 0; i < parameters.Length; i++)
                {
                    //BUG: does not take into account modifiers (like out, ref...)
                    if (!parameters[i].ParameterType.IsAssignableFrom(dp[i].ParameterType))
                        return false;
                }
                return true;
            }
        }
    }

    internal class DialogFilterInterceptor : IInterceptor
    {
        public void Intercept(IInvocation invocation)
        {
            if (invocation.MethodInvocationTarget.Name == nameof(IFilePicker.BrowseForFile) 
                &&  ((string)invocation.Arguments[2]).Contains("*.pdf;*.ai"))
            {
                var newFormat = $"*.pdf;*.ai;{string.Join(";", MagickImageReader.SupportedFormats.Select(x => $"*{x}"))}";
                invocation.Arguments[2] = ((string)invocation.Arguments[2]).Replace("*.pdf;*.ai", newFormat);
            }

            invocation.Proceed();
        }
    }
}
