﻿namespace EntityGraphQL.Schema
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using EntityGraphQL.Extensions;
    using EntityGraphQL.Schema.Models;

    public static class SchemaIntrospection
    {
        /// <summary>
        /// Creates an Introspection schema
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="typeMappings"></param>
        /// <returns></returns>
        public static Schema Make(ISchemaProvider schema)
        {
            var types = new List<TypeElement>
            {
                new TypeElement("OBJECT", schema.QueryContextName)
                {
                    Description = "The query type, represents all of the entry points into our object graph",
                    OfType = null,
                },
                new TypeElement( "OBJECT", "Mutation")
                {
                    Description = "The mutation type, represents all updates we can make to our data",
                    OfType = null,
                },
            };
            types.AddRange(BuildQueryTypes(schema));
            types.AddRange(BuildInputTypes(schema));
            types.AddRange(BuildEnumTypes(schema));
            types.AddRange(BuildScalarTypes(schema));

            var schemaDescription = new Schema(new TypeElement(null, schema.QueryContextName),
                new TypeElement(null, "Mutation"),
                null,
                types.OrderBy(x => x.Name).ToList(),
                BuildDirectives(schema)
            );

            return schemaDescription;
        }

        private static IEnumerable<TypeElement> BuildScalarTypes(ISchemaProvider schema)
        {
            var types = new List<TypeElement>();

            foreach (var customScalar in schema.GetScalarTypes())
            {
                var typeElement = new TypeElement("SCALAR", customScalar.Name);

                types.Add(typeElement);
            }

            return types;
        }

        private static List<TypeElement> BuildQueryTypes(ISchemaProvider schema)
        {
            var types = new List<TypeElement>();

            foreach (var st in schema.GetNonContextTypes().Where(s => !s.IsInput && !s.IsEnum && !s.IsScalar))
            {
                var typeElement = new TypeElement("OBJECT", st.Name)
                {
                    Description = st.Description
                };

                types.Add(typeElement);
            }

            return types;
        }

        /// <summary>
        /// Build INPUT Type to be used by Mutations
        /// </summary>
        /// <param name="schema"></param>
        /// <remarks>
        /// Since Types and Inputs cannot have the same name, camelCase the name to prevent duplicates.
        /// </remarks>
        /// <returns></returns>
        private static List<TypeElement> BuildInputTypes(ISchemaProvider schema)
        {
            var types = new List<TypeElement>();

            foreach (ISchemaType schemaType in schema.GetNonContextTypes().Where(s => s.IsInput))
            {
                if (schemaType.Name.StartsWith("__"))
                    continue;

                var inputValues = new List<InputValue>();
                foreach (Field field in schemaType.GetFields())
                {
                    if (field.Name.StartsWith("__"))
                        continue;

                    // Skip any property with special attribute
                    var property = schemaType.TypeDotnet.GetProperty(field.Name);
                    if (property != null && GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(property))
                        continue;

                    // Skipping custom fields added to schema
                    if (field.ResolveExpression?.NodeType == System.Linq.Expressions.ExpressionType.Call)
                        continue;

                    // Skipping ENUM type
                    if (field.ReturnType.TypeDotnet.IsEnum)
                        continue;

                    inputValues.Add(new InputValue(field.Name, BuildType(schema, field.ReturnType, field.ReturnType.TypeDotnet, true))
                    {
                        Description = field.Description,
                    });
                }

                var typeElement = new TypeElement("INPUT_OBJECT", schemaType.Name)
                {
                    Description = schemaType.Description,
                    InputFields = inputValues.ToArray()
                };

                types.Add(typeElement);
            }

            return types;
        }

        private static List<TypeElement> BuildEnumTypes(ISchemaProvider schema)
        {
            var types = new List<TypeElement>();

            foreach (ISchemaType schemaType in schema.GetNonContextTypes().Where(s => s.IsEnum))
            {
                var typeElement = new TypeElement("ENUM", schemaType.Name)
                {
                    Description = schemaType.Description,
                    EnumValues = new EnumValue[] { }
                };
                if (schemaType.Name.StartsWith("__"))
                    continue;

                var enumTypes = new List<EnumValue>();

                //filter to ENUM type ONLY!
                foreach (Field field in schemaType.GetFields())
                {
                    if (field.Name.StartsWith("__"))
                        continue;

                    enumTypes.Add(new EnumValue(field.Name)
                    {
                        Description = field.Description,
                        IsDeprecated = field.IsDeprecated,
                        DeprecationReason = field.DeprecationReason
                    });
                }

                typeElement.EnumValues = enumTypes.ToArray();
                if (typeElement.EnumValues.Count() > 0)
                    types.Add(typeElement);
            }

            return types;
        }

        private static TypeElement BuildType(ISchemaProvider schema, GqlTypeInfo typeInfo, Type clrType, bool isInput = false)
        {
            // Is collection of objects?
            var type = new TypeElement();
            if (clrType.IsEnumerableOrArray())
            {
                type.Kind = "LIST";
                type.Name = null;
                type.OfType = BuildType(schema, typeInfo, typeInfo.SchemaType.TypeDotnet, isInput);
            }
            else if (clrType.Name == "EntityQueryType`1")
            {
                type.Kind = "SCALAR";
                type.Name = "String";
                type.OfType = null;
            }
            else if (clrType.IsEnum)
            {
                type.Kind = "ENUM";
                type.Name = typeInfo.SchemaType.Name;
                type.OfType = null;
            }
            else
            {
                type.Kind = typeInfo.SchemaType.IsScalar ? "SCALAR" : "OBJECT";
                type.OfType = null;
                if (type.Kind == "OBJECT" && isInput)
                {
                    type.Kind = "INPUT_OBJECT";
                }
                type.Name = typeInfo.SchemaType.Name;
            }
            if (typeInfo.TypeNotNullable)
            {
                return new TypeElement("NON_NULL", null)
                {
                    OfType = type
                };
            }

            return type;
        }

        /// <summary>
        /// This is used in a lazy evaluated field as a graph can have circular dependencies
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="combinedMapping"></param>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public static Models.Field[] BuildFieldsForType(ISchemaProvider schema, string typeName)
        {
            if (typeName == "Query")
            {
                return BuildRootQueryFields(schema);
            }
            if (typeName == "Mutation")
            {
                return BuildMutationFields(schema);
            }

            var fieldDescs = new List<Models.Field>();
            if (!schema.HasType(typeName))
            {
                return fieldDescs.ToArray();
            }
            var type = schema.Type(typeName);
            foreach (var field in type.GetFields())
            {
                if (field.Name.StartsWith("__"))
                    continue;

                fieldDescs.Add(new Models.Field(schema.SchemaFieldNamer(field.Name), BuildType(schema, field.ReturnType, field.ReturnType.TypeDotnet))
                {
                    Args = BuildArgs(schema, field).ToArray(),
                    DeprecationReason = field.DeprecationReason,
                    Description = field.Description,
                    IsDeprecated = field.IsDeprecated,
                });
            }
            return fieldDescs.ToArray();
        }

        private static Models.Field[] BuildRootQueryFields(ISchemaProvider schema)
        {
            var rootFields = new List<Models.Field>();

            foreach (var field in schema.Type(schema.QueryContextName).GetFields())
            {
                if (field.Name.StartsWith("__"))
                    continue;

                // Skipping ENUM type
                if (field.ReturnType.TypeDotnet.IsEnum)
                    continue;

                //== Fields ==//
                rootFields.Add(new Models.Field(field.Name, BuildType(schema, field.ReturnType, field.ReturnType.TypeDotnet))
                {
                    Args = BuildArgs(schema, field).ToArray(),
                    IsDeprecated = field.IsDeprecated,
                    DeprecationReason = field.DeprecationReason,
                    Description = field.Description
                });
            }
            return rootFields.ToArray();
        }

        private static Models.Field[] BuildMutationFields(ISchemaProvider schema)
        {
            var rootFields = new List<Models.Field>();

            foreach (var field in schema.GetSchemaType(schema.MutationType, null).GetFields())
            {
                if (field.Name.StartsWith("__"))
                    continue;

                var args = BuildArgs(schema, field).ToArray();
                rootFields.Add(new Models.Field(field.Name, BuildType(schema, field.ReturnType, field.ReturnType.TypeDotnet))
                {
                    Args = args,
                    IsDeprecated = field.IsDeprecated,
                    DeprecationReason = field.DeprecationReason,
                    Description = field.Description
                });
            }
            return rootFields.ToArray();
        }

        private static List<InputValue> BuildArgs(ISchemaProvider schema, IField field)
        {
            var args = new List<InputValue>();
            foreach (var arg in field.Arguments)
            {
                var type = BuildType(schema, arg.Value.Type, arg.Value.Type.TypeDotnet, true);

                args.Add(new InputValue(arg.Key, type)
                {
                    DefaultValue = null,
                    Description = null,
                });
            }

            return args;
        }

        private static List<Directive> BuildDirectives(ISchemaProvider schema)
        {
            var directives = schema.GetDirectives().Select(directive => new Directive(directive.Name)
            {
                Description = directive.Description,
                Locations = new string[] { "FIELD", "FRAGMENT_SPREAD", "INLINE_FRAGMENT" },
                Args = directive.GetArguments(schema, schema.SchemaFieldNamer).Select(arg => new InputValue(arg.Name, BuildType(schema, arg.Type, arg.Type.TypeDotnet, true))
                {
                    Description = arg.Description,
                    DefaultValue = null,
                }).ToArray()
            }).ToList();

            return directives;
        }

    }
}
