using System.Collections.Generic;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Directives;

public class IncludeDirectiveProcessor : DirectiveProcessor<IncludeArguments>
{
    public override string Name => "include";
    public override string Description => "Directs the executor to include this field or fragment only when the `if` argument is true.";

    public override List<ExecutableDirectiveLocation> Location => [ExecutableDirectiveLocation.FIELD, ExecutableDirectiveLocation.FRAGMENT_SPREAD, ExecutableDirectiveLocation.INLINE_FRAGMENT];

    public override IGraphQLNode? VisitNode(ExecutableDirectiveLocation location, IGraphQLNode? node, object? arguments)
    {
        if (arguments is null)
            throw new EntityGraphQLException("Argument 'if' is required for @include directive");
        return ((IncludeArguments)arguments).If ? node : null;
    }
}

public class IncludeArguments
{
    [GraphQLField("if", "Included when true.")]
    public bool If { get; set; }
}
