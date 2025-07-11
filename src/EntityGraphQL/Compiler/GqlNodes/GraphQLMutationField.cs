using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

public class GraphQLMutationField : BaseGraphQLQueryField
{
    public MutationField MutationField { get; set; }

    public BaseGraphQLQueryField? ResultSelection { get; set; }

    public GraphQLMutationField(
        ISchemaProvider schema,
        string name,
        MutationField mutationField,
        Dictionary<string, object?>? args,
        Expression nextFieldContext,
        ParameterExpression rootParameter,
        IGraphQLNode parentNode
    )
        : base(schema, mutationField, name, nextFieldContext, rootParameter, parentNode, args)
    {
        this.MutationField = mutationField;
    }

    public Task<object?> ExecuteMutationAsync<TContext>(
        TContext context,
        IServiceProvider? serviceProvider,
        ParameterExpression? variableParameter,
        IArgumentsTracker? variablesToUse,
        ExecutionOptions executionOptions
    )
    {
        try
        {
            return MutationField.CallAsync(context, Arguments, serviceProvider, variableParameter, variablesToUse, executionOptions);
        }
        catch (EntityQuerySchemaException e)
        {
            throw new EntityQuerySchemaException($"Error applying mutation: {e.Message}", e);
        }
    }

    protected override Expression? GetFieldExpression(
        CompileContext compileContext,
        IServiceProvider? serviceProvider,
        List<GraphQLFragmentStatement> fragments,
        ParameterExpression? docParam,
        IArgumentsTracker? docVariables,
        ParameterExpression schemaContext,
        bool withoutServiceFields,
        Expression? replacementNextFieldContext,
        List<Type>? possibleNextContextTypes,
        bool contextChanged,
        ParameterReplacer replacer
    )
    {
        if (ResultSelection == null)
            throw new EntityGraphQLCompilerException($"Mutation {Name} should have a result selection");

        return ResultSelection.GetNodeExpression(
            compileContext,
            serviceProvider,
            fragments,
            docParam,
            docVariables,
            schemaContext,
            withoutServiceFields,
            replacementNextFieldContext,
            possibleNextContextTypes,
            contextChanged,
            replacer
        );
    }
}
