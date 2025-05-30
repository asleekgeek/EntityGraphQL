using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

public class GraphQLFragmentStatement : IGraphQLNode
{
    public Expression? NextFieldContext { get; }
    public IGraphQLNode? ParentNode { get; }
    public ParameterExpression? RootParameter { get; }

    public IField? Field { get; }
    public bool HasServices => Field?.Services.Count > 0;

    public IReadOnlyDictionary<string, object?> Arguments { get; }

    public string Name { get; }

    public List<BaseGraphQLField> QueryFields { get; } = [];

    public ISchemaProvider Schema { get; }
    public bool IsRootField => false;

    public GraphQLFragmentStatement(ISchemaProvider schema, string name, ParameterExpression selectContext, ParameterExpression rootParameter)
    {
        Name = name;
        NextFieldContext = selectContext;
        RootParameter = rootParameter;
        Arguments = new Dictionary<string, object?>();
        Schema = schema;
    }

    public void AddField(BaseGraphQLField field)
    {
        QueryFields.Add(field);
    }

    public void AddDirectives(IEnumerable<GraphQLDirective> graphQLDirectives)
    {
        throw new NotImplementedException();
    }
}
