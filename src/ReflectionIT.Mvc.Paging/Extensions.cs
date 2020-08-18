using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
//using Microsoft.AspNetCore.Mvc.ViewFeatures.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace ReflectionIT.Mvc.Paging {

    public static class Extensions {

#pragma warning disable IDE0022 // Use expression body for methods

        public static string DisplayNameFor<TModel, TValue>(this IHtmlHelper<PagingList<TModel>> html, Expression<Func<TModel, TValue>> expression) where TModel : class {
            return html.DisplayNameForInnerType<TModel, TValue>(expression);
        }

        [Obsolete("remove the pagingList parameter, it is not used any more")]
        public static IHtmlContent SortableHeaderFor<TModel, TValue>(this IHtmlHelper<PagingList<TModel>> html, Expression<Func<TModel, TValue>> expression, IPagingList pagingList) where TModel : class {
            var member = (expression.Body as MemberExpression).Member;
            return SortableHeaderFor(html, expression, member.Name, pagingList);
        }

        [Obsolete("remove the pagingList parameter, it is not used any more")]
        public static IHtmlContent SortableHeaderFor<TModel, TValue>(this IHtmlHelper<PagingList<TModel>> html, Expression<Func<TModel, TValue>> expression, string sortColumn, IPagingList pagingList) where TModel : class {
            return SortableHeaderFor(html, expression, sortColumn);
        }

        public static IHtmlContent SortableHeaderFor<TModel, TValue>(this IHtmlHelper<PagingList<TModel>> html, Expression<Func<TModel, TValue>> expression, string sortColumn) where TModel : class {
            var bldr = new HtmlContentBuilder();
            bldr.AppendHtml(html.ActionLink(html.DisplayNameForInnerType(expression), html.ViewData.Model.Action, html.ViewData.Model.GetRouteValueForSort(sortColumn)));
            IPagingList pagingList = html.ViewData.Model;

            if (pagingList.SortExpression == sortColumn || "-" + pagingList.SortExpression == sortColumn || pagingList.SortExpression == "-" + sortColumn) {
                bldr.AppendHtml(pagingList.SortExpression.StartsWith("-") ? PagingOptions.Current.HtmlIndicatorUp : PagingOptions.Current.HtmlIndicatorDown);
            }
            return bldr;
        }

        public static IHtmlContent SortableHeaderFor<TModel, TValue>(this IHtmlHelper<PagingList<TModel>> html, Expression<Func<TModel, TValue>> expression) where TModel : class {
            var member = (expression.Body as MemberExpression).Member;
            return SortableHeaderFor(html, expression, member.Name);
        }
#pragma warning restore IDE0022 // Use expression body for methods

        public static IQueryable<T> OrderBy<T>(this IQueryable<T> source, string sortExpression) where T : class {
            var index = 0;
            var a = sortExpression.Split(',');
            foreach (var item in a) {
                var m = index++ > 0 ? "ThenBy" : "OrderBy";
                if (item.StartsWith("-")) {
                    m += "Descending";
                    sortExpression = item.Substring(1);
                } else {
                    sortExpression = item;
                }

                var mc = GenerateMethodCall<T>(source, m, sortExpression.TrimStart());
                source = source.Provider.CreateQuery<T>(mc);
            }
            return source;
        }

        private static LambdaExpression GenerateSelector<TEntity>(string propertyName, out Type resultType) where TEntity : class {
            // Create a parameter to pass into the Lambda expression (Entity => Entity.OrderByField).
            var parameter = Expression.Parameter(typeof(TEntity), "Entity");
            //  create the selector part, but support child properties
            PropertyInfo property;
            Expression propertyAccess;
            if (propertyName.Contains('.')) {
                // support to be sorted on child fields.
                var childProperties = propertyName.Split('.');
                property = typeof(TEntity).GetProperty(childProperties[0]);
                propertyAccess = Expression.MakeMemberAccess(parameter, property);
                for (var i = 1; i < childProperties.Length; i++) {
                    property = property.PropertyType.GetProperty(childProperties[i]);
                    propertyAccess = Expression.MakeMemberAccess(propertyAccess, property);
                }
            } else {
                property = typeof(TEntity).GetProperty(propertyName);
                propertyAccess = Expression.MakeMemberAccess(parameter, property);
            }
            resultType = property.PropertyType;
            // Create the order by expression.
            return Expression.Lambda(propertyAccess, parameter);
        }

        private static MethodCallExpression GenerateMethodCall<TEntity>(IQueryable<TEntity> source, string methodName, string fieldName) where TEntity : class {
            var type = typeof(TEntity);
            var selector = GenerateSelector<TEntity>(fieldName, out var selectorResultType);
            var resultExp = Expression.Call(typeof(Queryable), methodName,
                                            new Type[] { type, selectorResultType },
                                            source.Expression, Expression.Quote(selector));
            return resultExp;
        }

        [Obsolete("This call is not required any move since we moved to Razor Class Libraries")]
        public static void AddPaging(this IServiceCollection services) {
            ////Get a reference to the assembly that contains the view components
            //var assembly = typeof(ReflectionIT.Mvc.Paging.PagerViewComponent).GetTypeInfo().Assembly;

            ////Create an EmbeddedFileProvider for that assembly
            //var embeddedFileProvider = new EmbeddedFileProvider(
            //    assembly, "ReflectionIT.Mvc.Paging"
            //);

            ////Add the file provider to the Razor view engine
            //services.Configure<RazorViewEngineOptions>(options => {
            //    options.FileProviders.Add(embeddedFileProvider);
            //});
        }

#pragma warning disable IDE0060 // Remove unused parameter
        public static void AddPaging(this IServiceCollection services, Action<PagingOptions> configureOptions) {
#pragma warning restore IDE0060 // Remove unused parameter
                               //AddPaging(services);
            configureOptions(PagingOptions.Current);
        }

        private static IHtmlContent MetaDataFor<TModel, TValue>(this IHtmlHelper<PagingList<TModel>> html, Expression<Func<TModel, TValue>> expression, Func<ModelMetadata, string> property) where TModel : class
        {
            if (html == null) throw new ArgumentNullException(nameof(html));
            if (expression == null) throw new ArgumentNullException(nameof(expression));

            ModelExpressionProvider modelExpressionProvider = (ModelExpressionProvider)html.ViewContext.HttpContext.RequestServices.GetService(typeof(IModelExpressionProvider));
            var modelMetadata = GetModelMetadata(modelExpressionProvider, new ViewDataDictionary<TModel>(html.ViewData, model: null), expression);

            return new HtmlString(property(modelMetadata));
        }
        
        private static ModelMetadata GetModelMetadata<TModel, TValue>(ModelExpressionProvider modelExpressionProvider, ViewDataDictionary<TModel> viewData, Expression<Func<TModel, TValue>> expression) 
        {
            var modelExpression = modelExpressionProvider.CreateModelExpression(viewData, expression);
            if (modelExpression == null) throw new InvalidOperationException($"Failed to get model expression for {modelExpressionProvider.GetExpressionText(expression)}");

            return modelExpression.Metadata;
        }
        
        private static string GenerateShortName(ModelMetadata modelMetadata)
        {
            if (modelMetadata != null)
            {
                ModelBinding.Metadata.DefaultModelMetadata defaultMetadata = (ModelBinding.Metadata.DefaultModelMetadata)modelMetadata;
                if (defaultMetadata != null)
                {
                    var displayAttribute = defaultMetadata.Attributes.Attributes
                        .OfType<DisplayAttribute>()
                        .FirstOrDefault();
                    if (displayAttribute != null)
                    {
                        return displayAttribute.ShortName ?? modelMetadata.DisplayName ?? modelMetadata.Name;
                    }
                }
                //Return a default value if the property doesn't have a DisplayAttribute
                return modelMetadata.DisplayName ?? modelMetadata.Name;
            }
            else
            {
                throw new ArgumentNullException(nameof(modelMetadata));
            }
        }
        
        public static IHtmlContent ShortNameFor<TModel, TValue>(this IHtmlHelper<PagingList<TModel>> html, Expression<Func<TModel, TValue>> expression) where TModel : class
        {
            return html.MetaDataFor(expression, m =>
            {
                return GenerateShortName(m);
            });
        }
        
        private static string ShortNameForInnerType<TModel, TValue>(this IHtmlHelper<PagingList<TModel>> html, Expression<Func<TModel, TValue>> expression) where TModel : class
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }                    

            ModelExpressionProvider modelExpressionProvider = (ModelExpressionProvider)html.ViewContext.HttpContext.RequestServices.GetService(typeof(IModelExpressionProvider));
            var modelExpression = modelExpressionProvider.CreateModelExpression(new ViewDataDictionary<TModel>(html.ViewData, model: null), expression);

            return GenerateShortName(modelExpression.ModelExplorer.Metadata);
        }
    
        public static IHtmlContent SortableShortNameFor<TModel, TValue>(this IHtmlHelper<PagingList<TModel>> html, Expression<Func<TModel, TValue>> expression, string sortColumn) where TModel : class
        {
            var bldr = new HtmlContentBuilder();
            bldr.AppendHtml(html.ActionLink(html.ShortNameForInnerType(expression), html.ViewData.Model.Action, html.ViewData.Model.GetRouteValueForSort(sortColumn)));
            IPagingList pagingList = html.ViewData.Model;

            if (pagingList.SortExpression == sortColumn || "-" + pagingList.SortExpression == sortColumn || pagingList.SortExpression == "-" + sortColumn)
            {
                bldr.AppendHtml(pagingList.SortExpression.StartsWith("-") ? PagingOptions.Current.HtmlIndicatorUp : PagingOptions.Current.HtmlIndicatorDown);
            }
            return bldr;
        }

        public static IHtmlContent SortableShortNameFor<TModel, TValue>(this IHtmlHelper<PagingList<TModel>> html, Expression<Func<TModel, TValue>> expression) where TModel : class
        {
            var member = (expression.Body as MemberExpression).Member;
            return SortableShortNameFor(html, expression, member.Name);
        }
    }
}
