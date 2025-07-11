using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class ToGraphQLSchemaStringTests
{
    [Fact]
    public void TestIgnoreWithSchema()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        schemaProvider.AddMutationsFrom<IgnoreTestMutations>();
        schemaProvider.Type<Album>().RemoveField("old");
        var schema = schemaProvider.ToGraphQLSchemaString();
        Assert.DoesNotContain("hiddenField", schema);
        // this exists as it is available for querying
        Assert.Contains(
            @"type Album {
	genre: Genre!
	hiddenInputField: String
	id: Int!
	name: String!
}",
            schema
        );
        // doesn't include the hidden input fields
        Assert.Contains("addAlbum(name: String!, genre: Genre!): Album", schema);
    }

    [Fact]
    public void TestIgnoreWithSchemaBuilder()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(new SchemaBuilderOptions() { IgnoreTypes = new[] { typeof(Album) }.ToHashSet() });
        var schema = schemaProvider.ToGraphQLSchemaString();
        Assert.DoesNotContain("album", schema);
    }

    [Fact]
    public void TestIgnoreEnumWithSchemaBuilder()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(new SchemaBuilderOptions() { IgnoreTypes = new[] { typeof(Genre) }.ToHashSet() });
        var schema = schemaProvider.ToGraphQLSchemaString();
        Assert.DoesNotContain("genre", schema);
    }

    [Fact]
    public void TestMutationWithListReturnType()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        schemaProvider.AddMutationsFrom<IgnoreTestMutations>();
        var schema = schemaProvider.ToGraphQLSchemaString();
        Assert.Contains("addAlbum2(name: String!, genre: Genre!): [Album!]", schema);
    }

    [Fact]
    public void TestNotNullTypes()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        schemaProvider.Type<Album>().RemoveField("old");
        schemaProvider.AddMutationsFrom<IgnoreTestMutations>();
        var schema = schemaProvider.ToGraphQLSchemaString();
        // this exists as it is not null
        Assert.Contains(
            @"type Album {
	genre: Genre!
	hiddenInputField: String
	id: Int!
	name: String!
}",
            schema
        );
    }

    [Fact]
    public void TestNullableEnumInType()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        var schema = schemaProvider.ToGraphQLSchemaString();
        // this exists as it is not null
        Assert.Contains(
            @"type Artist {
	id: Int!
	type: ArtistType
}",
            schema
        );
    }

    [Fact]
    public void TestNotNullArgs()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        schemaProvider.AddMutationsFrom<IgnoreTestMutations>();
        var schema = schemaProvider.ToGraphQLSchemaString();
        // this exists as it is not null
        Assert.Contains("addAlbum(name: String!, genre: Genre!): Album", schema);
    }

    [Fact]
    public void TestNotNullEnumerableElementByDefault()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        schemaProvider.AddMutationsFrom<IgnoreTestMutations>();
        var schema = schemaProvider.ToGraphQLSchemaString();
        // this exists as it is not null
        Assert.Contains("albums: [Album!]", schema);
    }

    [Fact]
    public void TestNullEnumerableElement()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        schemaProvider.AddMutationsFrom<IgnoreTestMutations>();
        var schema = schemaProvider.ToGraphQLSchemaString();
        // this exists as it is not null
        Assert.Contains("nullAlbums: [Album]", schema);
    }

    [Fact]
    public void TestDeprecatedField()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        var schema = schemaProvider.ToGraphQLSchemaString();
        // this exists as it is not null
        Assert.Contains("old: Int! @deprecated(reason: \"because\")", schema);
    }

    [Fact]
    public void TestDeprecatedMutationField()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        schemaProvider.AddMutationsFrom<IgnoreTestMutations>();
        var schema = schemaProvider.ToGraphQLSchemaString();
        // this exists as it is not null
        Assert.Contains("addAlbumOld(name: String!, genre: Genre!): Album! @deprecated(reason: \"This is obsolete\")", schema);
    }

    [Fact]
    public void TestDeprecatedEnumField()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        var schema = schemaProvider.ToGraphQLSchemaString();
        // this exists as it is not null
        Assert.Contains("Obsolete @deprecated(reason: \"This is an obsolete genre\")", schema);
    }

    [Fact]
    public void TestNullableRefTypeMutationField()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        schemaProvider.AddMutationsFrom<NullableRefTypeMutations>();
        var schema = schemaProvider.ToGraphQLSchemaString();
        // this exists as it is not null
        Assert.Contains("addAlbum(name: String!, genre: Genre!): Album", schema);
        Assert.DoesNotContain("addAlbum(name: String!, genre: Genre!): Album!", schema);
        Assert.Contains("addAlbum2(name: String!, genre: Genre!): Album!", schema);
        Assert.Contains("addAlbum3(name: String!, genre: Genre!): Album", schema);
        Assert.DoesNotContain("addAlbum3(name: String!, genre: Genre!): Album!", schema);

        var gql = new QueryRequest
        {
            Query =
                @"
                  query {
                    __type(name: ""Mutation"") {                        
                        fields {
                            name
                            type  { 
                                name
                                kind
                                ofType {
                                    name
                                    kind
                                }
                            }
                            args {
                                name 
                                type { name kind }
                            }
                        }
                    }
                  }
                ",
        };

        var res = schemaProvider.ExecuteRequestWithContext(gql, new IgnoreTestSchema(), null, null);
        Assert.Null(res.Errors);

        var mutation = (dynamic)res.Data!["__type"]!;

        Assert.Equal("addAlbum", mutation.fields[0].name);
        Assert.Equal("Album", mutation.fields[0].type.name);
        Assert.Equal("OBJECT", mutation.fields[0].type.kind);
        Assert.Equal((string?)null, mutation.fields[0].type.ofType);
        Assert.Equal("name", mutation.fields[0].args[0].name);
        Assert.Equal("NON_NULL", mutation.fields[0].args[0].type.kind);
        Assert.Equal("genre", mutation.fields[0].args[1].name);
        Assert.Equal("NON_NULL", mutation.fields[0].args[1].type.kind);

        Assert.Equal("addAlbum2", mutation.fields[1].name);
        Assert.Equal((string?)null, mutation.fields[1].type.name);
        Assert.Equal("NON_NULL", mutation.fields[1].type.kind);
        Assert.Equal("Album", mutation.fields[1].type.ofType.name);
        Assert.Equal("name", mutation.fields[1].args[0].name);
        Assert.Equal("NON_NULL", mutation.fields[1].args[0].type.kind);
        Assert.Equal("genre", mutation.fields[1].args[1].name);
        Assert.Equal("NON_NULL", mutation.fields[1].args[1].type.kind);

        Assert.Equal("addAlbum3", mutation.fields[2].name);
        Assert.Equal("Album", mutation.fields[2].type.name);
        Assert.Equal("OBJECT", mutation.fields[2].type.kind);
        Assert.Equal((string?)null, mutation.fields[2].type.ofType);
        Assert.Equal("name", mutation.fields[2].args[0].name);
        Assert.Equal("NON_NULL", mutation.fields[2].args[0].type.kind);
        Assert.Equal("genre", mutation.fields[2].args[1].name);
        Assert.Equal("NON_NULL", mutation.fields[2].args[1].type.kind);
    }

    [Fact]
    public void TestAbstractClassToGraphQLSchemaString()
    {
        var schemaProvider = SchemaBuilder.FromObject<AbstractClassTestSchema>();

        schemaProvider.AddType<AbstractClassTestSchema.Dog>("Dogs are animals").ImplementAllBaseTypes().AddAllFields();
        schemaProvider.AddType<AbstractClassTestSchema.Cat>("Cats are animals").Implements<Animal>().AddAllFields();
        schemaProvider.AddType<AbstractClassTestSchema.Fish>("Fish are animals");

        schemaProvider.UpdateType<AbstractClassTestSchema.Fish>(x => x.Implements("Animal").AddAllFields());

        var schema = schemaProvider.ToGraphQLSchemaString();
        // this exists as it is not null
        Assert.Contains(@"interface Animal", schema);

        Assert.Contains(@"type Cat implements Animal", schema);
        Assert.Contains(@"type Dog implements Animal", schema);
        Assert.Contains(@"type Fish implements Animal", schema);
    }

    [Fact]
    public void TestMultipleInheritanceToGraphQLSchemaString()
    {
        var schemaProvider = SchemaBuilder.FromObject<AbstractClassTestSchema>();

        schemaProvider.AddType<AbstractClassTestSchema.ISwim>("").AddAllFields();
        schemaProvider.AddType<AbstractClassTestSchema.Dog>("Dogs are animals").ImplementAllBaseTypes().AddAllFields();
        schemaProvider.AddType<AbstractClassTestSchema.Cat>("Cats are animals").Implements<Animal>().AddAllFields();
        schemaProvider.AddType<AbstractClassTestSchema.Fish>("Fish are animals").ImplementAllBaseTypes().AddAllFields();

        var schema = schemaProvider.ToGraphQLSchemaString();
        // this exists as it is not null
        Assert.Contains(@"interface Animal", schema);
        Assert.Contains(@"interface ISwim", schema);

        Assert.Contains(@"type Cat implements Animal", schema);
        Assert.Contains(@"type Dog implements Animal", schema);
        Assert.Contains(@"type Fish implements Animal & IAnimal & ISwim", schema);
    }

    [Fact]
    public void TestNoMutations()
    {
        var schemaProvider = SchemaBuilder.FromObject<AbstractClassTestSchema>();

        var schema = schemaProvider.ToGraphQLSchemaString();
        Assert.DoesNotContain("mutation:", schema);
        Assert.DoesNotContain($"type {schemaProvider.Mutation().SchemaType.Name}", schema);
    }

    [Fact]
    public void TestOutputsScalarDescription()
    {
        var schemaProvider = SchemaBuilder.FromObject<AbstractClassTestSchema>();

        var schema = schemaProvider.ToGraphQLSchemaString();
        Assert.Contains("\"\"\"Date with time scalar\"\"\"", schema);
    }

    [Fact]
    public void TestGetArgDefaultValue_Null_NotSet()
    {
        Assert.Equal("", SchemaGenerator.GetArgDefaultValue(new DefaultArgValue(false, null), (e) => e));
    }

    [Fact]
    public void TestGetArgDefaultValue_Null()
    {
        Assert.Equal("null", SchemaGenerator.GetArgDefaultValue(new DefaultArgValue(true, null), (e) => e));
    }

    [Fact]
    public void TestGetArgDefaultValue_DbNull()
    {
        Assert.Equal("null", SchemaGenerator.GetArgDefaultValue(new DefaultArgValue(true, DBNull.Value), (e) => e));
    }

    [Fact]
    public void TestGetArgDefaultValue_Int()
    {
        Assert.Equal("3", SchemaGenerator.GetArgDefaultValue(new DefaultArgValue(true, 3), (e) => e));
    }

    [Fact]
    public void TestGetArgDefaultValue_Decimal()
    {
        Assert.Equal("3.14", SchemaGenerator.GetArgDefaultValue(new DefaultArgValue(true, 3.14), (e) => e));
    }

    [Fact]
    public void TestGetArgDefaultValue_String()
    {
        Assert.Equal("\"stringValue\"", SchemaGenerator.GetArgDefaultValue(new DefaultArgValue(true, "stringValue"), (e) => e));
    }

    [Fact]
    public void TestGetArgDefaultValue_String_Empty()
    {
        Assert.Equal("\"\"", SchemaGenerator.GetArgDefaultValue(new DefaultArgValue(true, ""), (e) => e));
    }

    [Fact]
    public void TestGetArgDefaultValue_IntArray()
    {
        Assert.Equal("[1, 2, 3]", SchemaGenerator.GetArgDefaultValue(new DefaultArgValue(true, new[] { 1, 2, 3 }), (e) => e));
    }

    [Fact]
    public void TestGetArgDefaultValue_StringArray()
    {
        Assert.Equal("[\"one\", \"two\", \"three\"]", SchemaGenerator.GetArgDefaultValue(new DefaultArgValue(true, new[] { "one", "two", "three" }), (e) => e));
    }

    [Fact]
    public void TestGetArgDefaultValue_Enum()
    {
        Assert.Equal("Alternitive", SchemaGenerator.GetArgDefaultValue(new DefaultArgValue(true, Genre.Alternitive), (e) => e));
    }

    [Fact]
    public void TestGetArgDefaultValue_Filter()
    {
        Assert.Equal("", SchemaGenerator.GetArgDefaultValue(new DefaultArgValue(true, new EntityQueryType<Task>()), (e) => e));
    }

    [Fact]
    public void TestGetArgDefaultValue_Object()
    {
        Assert.Equal("{ Id: 5, Name: \"Test\", Genre: Rock, Old: 0 }", SchemaGenerator.GetArgDefaultValue(new DefaultArgValue(true, new Album { Id = 5, Name = "Test" }), (e) => e));
    }
}

public class IgnoreTestMutations
{
    [GraphQLMutation]
    public Expression<Func<IgnoreTestSchema, Album>> AddAlbum(IgnoreTestSchema db, Album args)
    {
        var newAlbum = new Album { Id = new Random().Next(100), Name = args.Name };
        db.Albums.Add(newAlbum);
        return ctx => ctx.Albums.First(a => a.Id == newAlbum.Id);
    }

    [GraphQLMutation("Test correct generation of return type for a list")]
    public Expression<Func<IgnoreTestSchema, IEnumerable<Album>>> AddAlbum2(IgnoreTestSchema db, Album args)
    {
        var newAlbum = new Album { Id = new Random().Next(100), Name = args.Name };
        db.Albums.Add(newAlbum);
        return ctx => ctx.Albums;
    }

    [GraphQLMutation]
    [Obsolete("This is obsolete")]
    public Expression<Func<IgnoreTestSchema, Album>> AddAlbumOld(IgnoreTestSchema db, Album args)
    {
        var newAlbum = new Album { Id = new Random().Next(100), Name = args.Name };
        db.Albums.Add(newAlbum);
        return ctx => ctx.Albums.First(a => a.Id == newAlbum.Id);
    }
}

public class NullableRefTypeMutations
{
    [GraphQLMutation]
    public Expression<Func<IgnoreTestSchema, Album?>> AddAlbum(IgnoreTestSchema db, Album args)
    {
        var newAlbum = new Album { Id = new Random().Next(100), Name = args.Name };
        db.Albums.Add(newAlbum);
        return ctx => ctx.Albums.FirstOrDefault(a => a.Id == newAlbum.Id);
    }

    [GraphQLMutation]
    public Expression<Func<IgnoreTestSchema, Album>> AddAlbum2(IgnoreTestSchema db, Album args)
    {
        var newAlbum = new Album { Id = new Random().Next(100), Name = args.Name };
        db.Albums.Add(newAlbum);
        return ctx => ctx.Albums.First(a => a.Id == newAlbum.Id);
    }

    [GraphQLMutation]
    public Expression<Func<IgnoreTestSchema, Album?>> AddAlbum3(IgnoreTestSchema db, Album args)
    {
        var newAlbum = new Album { Id = new Random().Next(100), Name = args.Name };
        db.Albums.Add(newAlbum);
        return ctx => ctx.Albums.First(a => a.Id == newAlbum.Id);
    }
}

public class MovieArgs
{
    [GraphQLNotNull]
    public string Name { get; set; } = string.Empty;

    [GraphQLIgnore(GraphQLIgnoreType.Input)]
    public string Hidden { get; set; } = string.Empty;
}

public class AbstractClassTestSchema
{
    public List<Animal> Animals { get; set; } = [];

    public interface ISwim
    {
        public int Fins { get; set; }
    }

    public class Cat : Animal
    {
        public int Lives { get; set; }
    }

    public class Dog : Animal
    {
        public int Bones { get; set; }
    }

    public class Fish : Animal, ISwim
    {
        public int Fins { get; set; }
    }
}

public class IgnoreTestSchema
{
    public IgnoreTestSchema()
    {
        Movies = [];
        Albums = [];
        NullAlbums = [];
        Artists = [];
    }

    [GraphQLIgnore(GraphQLIgnoreType.Query)]
    public List<Movie> Movies { get; set; }
    public List<Album> Albums { get; set; }

    [GraphQLElementTypeNullableAttribute]
    public List<Album> NullAlbums { get; set; }
    public List<Artist> Artists { get; set; }
}

public enum Genre
{
    Rock,
    Classical,
    Jazz,
    Alternitive,
    Pop,

    [Obsolete("This is an obsolete genre")]
    Obsolete,
}

[GraphQLArguments]
public class Album
{
    [GraphQLIgnore(GraphQLIgnoreType.Input)]
    public int Id { get; set; }

    [GraphQLNotNull]
    public string Name { get; set; } = string.Empty;

    [GraphQLIgnore(GraphQLIgnoreType.Input)]
    public string? HiddenInputField { get; set; }

    [GraphQLIgnore(GraphQLIgnoreType.All)] // default
    public string? HiddenAllField { get; set; }

    [GraphQLNotNull]
    public Genre Genre { get; set; }

    [Obsolete("because")]
    [GraphQLIgnore(GraphQLIgnoreType.Input)]
    public int Old { get; set; }
}

public class Movie
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

public enum ArtistType
{
    Solo,
    Band,
    Supergroup,
}

public class Artist
{
    public int Id { get; set; }
    public ArtistType? Type { get; set; }
}
