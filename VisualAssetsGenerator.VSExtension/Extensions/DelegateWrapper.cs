using System;
using System.Linq;
using System.Reflection;
using Castle.DynamicProxy;
using Microsoft.VisualStudio.DesignTools.ImageSet;

namespace VisualAssetGenerator.Extensions
{
    public class DelegateWrapper
    {
        public static object WrapAs(Delegate d1, Delegate d2, Delegate d3, ISelfIdentifyingInterceptor selfIdentifyingInterceptor, Type interfaceType)
        {
            var generator = new ProxyGenerator();
            var options = new ProxyGenerationOptions { Selector = new DelegateSelector()};
            var proxy = generator.CreateInterfaceProxyWithoutTarget(
                interfaceType,
                new Type[0],
                options,
                new MethodInterceptor(d1),
                new MethodInterceptor(d2),
                new MethodInterceptor(d3),
                selfIdentifyingInterceptor);
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

        public interface ISelfIdentifyingInterceptor : IInterceptor
        {
            bool IsApplicableFor(Type type, MethodInfo method);
        }

        internal class MethodByNameAndEnumReturnType : ISelfIdentifyingInterceptor
        {
            private readonly string _methodName;
            private readonly string _enumName;
            private readonly string _returnValue;

            public MethodByNameAndEnumReturnType(string methodName, string enumName, string returnValue)
            {
                _methodName = methodName;
                _enumName = enumName;
                _returnValue = returnValue;
            }

            public bool IsApplicableFor(Type type, MethodInfo method)
            {
                return method.Name == _methodName &&
                       method.ReturnType.Name == _enumName &&
                       method.ReturnType.IsEnum;
            }

            public void Intercept(IInvocation invocation)
            {
                var returnType = invocation.Method.ReturnType;

                invocation.ReturnValue = Enum.Parse(returnType, _returnValue);
            }
        }

        internal class DelegateSelector : IInterceptorSelector
        {
            public IInterceptor[] SelectInterceptors(Type type, MethodInfo method, IInterceptor[] interceptors)
            {
                foreach (var interceptor in interceptors.OfType<MethodInterceptor>())
                {
                    var d = interceptor.Delegate;

                    if (IsEquivalent(d, method))
                        return new IInterceptor[] { interceptor };
                }

                return interceptors
                    .Where(x => (x as ISelfIdentifyingInterceptor)?.IsApplicableFor(type, method) == true)
                    .ToArray();
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

                return !parameters.Where((t, i) => !t.ParameterType.IsAssignableFrom(dp[i].ParameterType)).Any();
            }
        }
    }

    internal class DialogFilterInterceptor : IInterceptor
    {
        public void Intercept(IInvocation invocation)
        {
            var formats = new[]
            {
                "*.pdf;*.ai",
                "*.ai;*.pdf"
            };

            if (invocation.MethodInvocationTarget.Name != nameof(IFilePicker.BrowseForFile)) return;

            var existingFormat = formats.FirstOrDefault(x => ((string) invocation.Arguments[2]).Contains(x));

            if (existingFormat == null) return;
            
            var newFormat = $"{existingFormat};{string.Join(";", MagickImageReader.SupportedFormats.Select(x => $"*{x}"))}";
            invocation.Arguments[2] = ((string) invocation.Arguments[2]).Replace(existingFormat, newFormat);

            invocation.Proceed();
        }
    }

    internal class ImageGeneratorInterceptor : IInterceptor
    {
        public void Intercept(IInvocation invocation)
        {
            if (invocation.TargetType.Name == "ImageGenerator")
            {
                switch (invocation.MethodInvocationTarget.Name)
                {
                    case "GenerateAsync":
                    {
                        var targetRoot = (ImageSetTarget) invocation.Arguments[0];

                        if (targetRoot.Source != null && !targetRoot.Source.IsVector &&
                            MagickImageReader.SupportedFormats.Contains(targetRoot.Source.Extension,
                                StringComparer.CurrentCultureIgnoreCase))
                        {
                            var isVectorField = typeof(ImageSetSource).GetField($"<{nameof(ImageSetSource.IsVector)}>k__BackingField",
                                BindingFlags.Instance | BindingFlags.NonPublic);

                            isVectorField?.SetValue(targetRoot.Source, true);
                        }

                        break;
                    }
                    case "get_FileExtension" when invocation.TargetType.Name == "ImageGenerator":
                        invocation.ReturnValue = ".png";
                        break;
                }
            }

            invocation.Proceed();
        }
    }
}
