﻿using System.Net.Http;
using System.Threading.Tasks;

using WebAnchor.RequestFactory;
using WebAnchor.RequestFactory.HttpAttributes;
using WebAnchor.RequestFactory.Transformation.Transformers;
using WebAnchor.RequestFactory.Transformation.Transformers.Default;

namespace WebAnchor.Tests.PayloadDependentUrlSegments
{
    [BaseLocation("/api")]
    [TypeNameAsUrlParameterList]
    public interface IDynamicTypedApi<in T>
    {
        [Post("/{type}")]
        Task<HttpResponseMessage> PostThis([Content]T t);
    }

    [BaseLocation("/api")]
    [TypeNameAsUrlParameter2]
    public interface IDynamicTypedApi2<in T>
    {
        [Post("/{type}")]
        Task<HttpResponseMessage> PostThis([Content]T t);
    }
}