﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;

using Castle.Core.Internal;
using Castle.DynamicProxy;

using WebAnchor.RequestFactory.Transformation;
using WebAnchor.RequestFactory.Transformation.Transformers.Attribute;
using WebAnchor.RequestFactory.Transformation.Transformers.Attribute.List;
using WebAnchor.RequestFactory.Transformation.Transformers.Default;
using WebAnchor.RequestFactory.Transformation.Transformers.Formattable;
using WebAnchor.RequestFactory.Transformation.Transformers.List;

namespace WebAnchor.RequestFactory
{
    public class HttpRequestFactory : IHttpRequestFactory
    {
        public HttpRequestFactory(IContentSerializer contentSerializer)
        {
            ContentSerializer = contentSerializer;
            DefaultParameterListTransformers = CreateDefaultTransformers() ?? new List<IParameterListTransformer>();
        }

        public IContentSerializer ContentSerializer { get; set; }
        public List<IParameterListTransformer> DefaultParameterListTransformers { get; set; }
        public Parameters ResolvedParameters { get; set; }

        public virtual HttpRequestMessage Create(IInvocation invocation)
        {
            var resolvedParameters = ResolveParameters(invocation);
            ResolvedParameters = new Parameters(
                resolvedParameters.Where(x => x.ParameterType == ParameterType.Route),
                resolvedParameters.Where(x => x.ParameterType == ParameterType.Query),
                resolvedParameters.FirstOrDefault(x => x.ParameterType == ParameterType.Content));
             
            var resolvedUrl = ResolveUrlRoute(invocation);
            var resolvedMethod = ResolveHttpMethod(invocation);

            var request = new HttpRequestMessage(resolvedMethod, resolvedUrl);

            if (resolvedMethod == HttpMethod.Post || resolvedMethod == HttpMethod.Put)
            {
                request.Content = ResolveContent(invocation);
            }

            return request;
        }

        protected virtual List<Parameter> ResolveParameters(IInvocation invocation)
        {
            var methodInfo = invocation.Method;
            var url = methodInfo.GetCustomAttribute<HttpAttribute>().URL;

            var invocationParameters =
               methodInfo.GetParameters()
                   .Select((x, i) => new { Index = i, ParameterInfo = x })
                   .Where(x => invocation.GetArgumentValue(x.Index) != null)
                   .Select(x => new Parameter(x.ParameterInfo, invocation.GetArgumentValue(x.Index), ResolveParameterType(x.ParameterInfo, url)))
                   .ToList();

            var transformedParameters = DefaultParameterListTransformers.Aggregate(invocationParameters,
                (current, transformer) => transformer.TransformParameters(current, new ParameterTransformContext(methodInfo))
                                                     .ToList());

            return transformedParameters;
        }

        protected virtual List<IParameterListTransformer> CreateDefaultTransformers()
        {
            return new List<IParameterListTransformer>
            {
                new ParameterOfListTransformer(),
                new DefaultParameterResolver(),
                new FormattableParameterResolver(),
                new ParameterListTransformerAttributeTransformer(),
                new ParameterTransformerAttributeTransformer(),
            };
        }

        protected virtual HttpContent ResolveContent(IInvocation invocation)
        {
            return ContentSerializer.Serialize(ResolvedParameters.Content);
        }

        protected virtual string ResolveUrlRoute(IInvocation invocation)
        {
            var methodInfo = invocation.Method;
            var methodAttribute = methodInfo.GetCustomAttribute<HttpAttribute>();
            var baseAttribute = methodInfo.DeclaringType.GetCustomAttribute<BaseLocationAttribute>();

            var substitutedUrl = methodAttribute.URL.Replace(
                    ResolvedParameters.RouteParameters.ToDictionary(x => CreateRouteSegmentId(x.Name), CreateRouteSegmentValue));

            var resolvedUrl = (baseAttribute != null ? baseAttribute.BaseUrl : string.Empty) + substitutedUrl;
            resolvedUrl = AppendUrlParams(resolvedUrl, ResolvedParameters.QueryParameters);
            return resolvedUrl;
        }

        protected virtual string AppendUrlParams(string url, IEnumerable<Parameter> queryParameters)
        {
            var urlParams = ResolvedParameters.QueryParameters.Any()
                                ? (url.Contains("?") ? "&" : "?")
                                  + string.Join("&", ResolvedParameters.QueryParameters.Select(CreateUrlParameter))
                                : string.Empty;
            return url + urlParams;
        }

        protected virtual HttpMethod ResolveHttpMethod(IInvocation invocation)
        {
            var methodInfo = invocation.Method;
            var methodAttribute = methodInfo.GetCustomAttribute<HttpAttribute>();
            var resolvedMethod = methodAttribute.Method;
            return resolvedMethod;
        }

        protected virtual string CreateRouteSegmentId(string name)
        {
            return "{" + name + "}";
        }

        protected virtual string CreateRouteSegmentValue(Parameter parameter)
        {
            var value = parameter.Value != null
                           ? parameter.Value.ToString()
                           : parameter.ParameterValue.ToString();
            return WebUtility.UrlEncode(value);
        }

        protected virtual string CreateUrlParameter(Parameter parameter)
        {
            var value = parameter.Value != null
                            ? parameter.Value.ToString()
                            : parameter.ParameterValue.ToString();
            return string.Format("{0}={1}", parameter.Name, WebUtility.UrlEncode(value));
        }

        private ParameterType ResolveParameterType(ParameterInfo parameterInfo, string url)
        {
            return parameterInfo.HasAttribute<ContentAttribute>()
                       ? ParameterType.Content
                       : (url.Contains(CreateRouteSegmentId(parameterInfo.Name))
                            ? ParameterType.Route
                            : ParameterType.Query);
        }
    }
}
