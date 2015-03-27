﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nancy.Routing;
using Swagger.ObjectModel;
using Swagger.ObjectModel.Builders;

namespace Nancy.Swagger
{

    [SwaggerApi]
    public static class SwaggerExtensionsTwo
    {
        public static T ToDataType<T>(this Type type, bool isTopLevel = false)
            where T : DataType, new()
        {
            var dataType = new T();

            if (type == null)
            {
                dataType.Type = "void";

                return dataType;
            }

            if (Primitive.IsPrimitive(type))
            {
                var primitive = Primitive.FromType(type);

                dataType.Format = primitive.Format;
                dataType.Type = primitive.Type;

                return dataType;
            }

            if (type.IsContainer())
            {
                dataType.Type = "array";

                var itemsType = type.GetElementType() ?? type.GetGenericArguments().FirstOrDefault();

                if (Primitive.IsPrimitive(itemsType))
                {
                    var primitive = Primitive.FromType(itemsType);

                    dataType.Items = new Item
                    {
                        Type = primitive.Type,
                        Format = primitive.Format
                    };

                    return dataType;
                }

                dataType.Items = new Item { Ref = SwaggerConfig.ModelIdConvention(itemsType) };

                return dataType;
            }

            if (isTopLevel)
            {
                dataType.Ref = SwaggerConfig.ModelIdConvention(type);
                return dataType;
            }

            dataType.Type = SwaggerConfig.ModelIdConvention(type);

            return dataType;
        }

        public static IEnumerable<Model> ToModel(this SwaggerModelData model, IEnumerable<Schema> knownModels = null)
        {
            var classProperties = model.Properties.Where(x => !Primitive.IsPrimitive(x.Type) && !x.Type.IsEnum && !x.Type.IsGenericType);

            var modelsData = knownModels ?? Enumerable.Empty<Schema>();

            foreach (var swaggerModelPropertyData in classProperties)
            {
                var properties = GetPropertiesFromType(swaggerModelPropertyData.Type);

                var modelDataForClassProperty =
                    modelsData.FirstOrDefault(x => x.ModelType == swaggerModelPropertyData.Type);

                var id = modelDataForClassProperty == null
                    ? swaggerModelPropertyData.Type.Name
                    : SwaggerConfig.ModelIdConvention(modelDataForClassProperty.ModelType);

                var description = modelDataForClassProperty == null
                    ? swaggerModelPropertyData.Description
                    : modelDataForClassProperty.Description;

                var required = modelDataForClassProperty == null
                    ? properties.Where(p => p.Required || p.Type.IsImplicitlyRequired())
                        .Select(p => p.Name)
                        .OrderBy(name => name)
                        .ToList()
                    : modelDataForClassProperty.Properties
                        .Where(p => p.Required || p.Type.IsImplicitlyRequired())
                        .Select(p => p.Name)
                        .OrderBy(name => name)
                        .ToList();

                var modelproperties = modelDataForClassProperty == null
                    ? properties.OrderBy(x => x.Name).ToDictionary(p => p.Name, ToModelProperty)
                    : modelDataForClassProperty.Properties.OrderBy(x => x.Name)
                        .ToDictionary(p => p.Name, ToModelProperty);

                yield return new Model
                {
                    Id = id,
                    Description = description,
                    Required = required,
                    Properties = modelproperties
                };
            }

            var topLevelModel = new Model
            {
                Id = SwaggerConfig.ModelIdConvention(model.ModelType),
                Description = model.Description,
                Required = model.Properties
                    .Where(p => p.Required || p.Type.IsImplicitlyRequired())
                    .Select(p => p.Name)
                    .OrderBy(name => name)
                    .ToList(),
                Properties = model.Properties
                    .OrderBy(p => p.Name)
                    .ToDictionary(p => p.Name, ToModelProperty)

                // TODO: SubTypes and Discriminator
            };

            yield return topLevelModel;
        }

        public static ModelProperty ToModelProperty(this SwaggerModelPropertyData modelPropertyData)
        {
            var propertyType = modelPropertyData.Type;

            var isClassProperty = !Primitive.IsPrimitive(propertyType);

            var modelProperty = modelPropertyData.Type.ToDataType<ModelProperty>(isClassProperty);

            modelProperty.Default = modelPropertyData.DefaultValue;
            modelProperty.Description = modelPropertyData.Description;
            modelProperty.Enum = modelPropertyData.Enum;
            modelProperty.Minimum = modelPropertyData.Minimum;
            modelProperty.Maximum = modelPropertyData.Maximum;

            if (modelPropertyData.Type.IsContainer())
            {
                modelProperty.UniqueItems = modelPropertyData.UniqueItems ? true : (bool?)null;
            }

            return modelProperty;
        }

        private static IList<SwaggerModelPropertyData> GetPropertiesFromType(Type type)
        {
            return type.GetProperties()
                .Select(property => new SwaggerModelPropertyData
                {
                    Name = property.Name,
                    Type = property.PropertyType
                }).ToList();
        }

        public static bool IsContainer(this Type type)
        {
            return typeof(IEnumerable).IsAssignableFrom(type)
                && !typeof(string).IsAssignableFrom(type);
        }


        internal static bool IsImplicitlyRequired(this Type type)
        {
            return type.IsValueType && !IsNullable(type);
        }

        internal static bool IsNullable(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static HttpMethod ToHttpMethod(this string method)
        {
            switch (method)
            {
                case "DELETE":
                    return HttpMethod.Delete;

                case "GET":
                    return HttpMethod.Get;

                case "OPTIONS":
                    return HttpMethod.Options;

                case "PATCH":
                    return HttpMethod.Patch;

                case "POST":
                    return HttpMethod.Post;

                case "PUT":
                    return HttpMethod.Put;

                default:
                    throw new NotSupportedException(string.Format("HTTP method '{0}' is not supported.", method));
            }
        }
    }
}