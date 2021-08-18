[![NuGet Package](https://img.shields.io/nuget/v/AndreasDorfer.BaseTypes.svg)](https://www.nuget.org/packages/AndreasDorfer.BaseTypes/)
# AD.BaseTypes
Fight primitive obsession and create expressive domain models with source generators.
## NuGet Package
    PM> Install-Package AndreasDorfer.BaseTypes -Version 0.4.0
## TL;DR
A succinct way to create wrappers around primitive types with records and source generators.
```csharp
using AD.BaseTypes;

[IntRange(0, 100)] partial record Rating;

Rating ok = new(75);

try
{
    Rating invalid = new(125);
}
catch (ArgumentOutOfRangeException ex) { /* ... */ }
```
## Motivation
Consider the following snippet:
```csharp
class Employee
{
    public string Id { get; }
    public string DepartmentId { get; }
    //more properties
    
    public Department GetDepartment() =>
        departmentRepository.Load(DepartmentId);
}

interface IDepartmentRepository
{
    Department Load(string id);
}
```
Both the employee's ID and the associated department's ID are modeled as strings ... although they are logically separate and must never be mixed. What if you accidentally use the wrong ID in `GetDepartment`?
```csharp
public Department GetDepartment() =>
    departmentRepository.Load(Id);
```
Your code still compiles. Hopefully, you've got some tests to catch that bug. But why not utilize the type system to prevent that bug in the first place?

You can use [records](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record) like [single case discriminated unions](https://fsharpforfunandprofit.com/posts/designing-with-types-single-case-dus/):
```csharp
sealed record EmployeeId(string Value);
sealed record DepartmentId(string Value);

class Employee
{
    public EmployeeId Id { get; }
    public DepartmentId DepartmentId { get; }
    //more properties
    
    public Department GetDepartment() =>
        departmentRepository.Load(DepartmentId);
}

interface IDepartmentRepository
{
    Department Load(DepartmentId id);
}
```
Now, you get a compiler error when you accidentally use the employee's ID instead of the department's ID. Great! But there's more bugging me: both the employee's and the department's ID must not be empty. The records could reflect that constraint like this:
```csharp
sealed record EmployeeId
{
    public EmployeeId(string value)
    {
        if(string.IsNullOrEmpty(value)) throw new ArgumentException("must not be empty");
        Value = value;
    }
    public string Value { get; }
}
sealed record DepartmentId
{
    public DepartmentId(string value)
    {
        if(string.IsNullOrEmpty(value)) throw new ArgumentException("must not be empty");
        Value = value;
    }
    public string Value { get; }
}
```
You get an `ArgumentException` whenever you try to create an empty ID. But that's a lot of boilerplate code. There sure is a solution to that:
##  Source Generation
With `AD.BaseTypes` you can write the records like this:
```csharp
[NonEmptyString] partial record EmployeeId;
[NonEmptyString] partial record DepartmentId;
```
**That's it!** All the boilerplate code is [generated](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview) for you. Here's what the *generated* code for `EmployeeId` looks like:
```csharp
[TypeConverter(typeof(BaseTypeConverter<EmployeeId, string>))]
[JsonConverter(typeof(BaseTypeJsonConverter<EmployeeId, string>))]
sealed partial record EmployeeId : IComparable<EmployeeId>, IComparable, IBaseType<string>
{
    public EmployeeId(string value)
    {
        new NonEmptyStringAttribute().Validate(value);
        Value = value;
    }
    public string Value { get; }
    public override string ToString() => Value.ToString();
    public int CompareTo(object? obj) => CompareTo(obj as EmployeeId);
    public int CompareTo(EmployeeId? other) => other is null ? 1 : Comparer<string>.Default.Compare(Value, other.Value);
    public static implicit operator string(EmployeeId item) => item.Value;
    public static EmployeeId Create(string value) => new(value);
}
```
## But there's more!
Let's say you need to model a name that's from 1 to 20 characters long:
```csharp
[MinMaxLength(1, 20)] partial record Name;
```
Or you need to model a serial number that must follow a certain pattern:
```csharp
[Regex(@"^\d\d-\w\w\w\w$")] partial record SerialNumber;
```
## Included Attributes
The included attributes are:
- `BoolAttribute`
- `DateTimeAttribute`
- `DecimalAttribute`
- `DoubleAttribute`
- `GuidAttribute`
- `IntAttribute`
- `IntMaxAttribute`
- `IntMinAttribute`
- `IntRangeAttribute`
- `MaxLengthAttribute`
- `MinLengthAttribute`
- `MinMaxLengthAttribute`
- `NonEmptyStringAttribute`
- `PositiveDecimalAttribute`
- `RegexAttribute`
- `StringAttribute`
## JSON Serialization
The generated types are transparent to the serializer. They are serialized like the types they wrap.
## Custom Attributes
You can create custom attributes. Let's say you need a `DateTime` only for weekends:
```csharp
[AttributeUsage(AttributeTargets.Class)]
class WeekendAttribute : Attribute, IBaseTypeValidation<DateTime>
{
    public void Validate(DateTime value)
    {
        if (value.DayOfWeek != DayOfWeek.Saturday && value.DayOfWeek != DayOfWeek.Sunday)
            throw new ArgumentOutOfRangeException(nameof(value), value, "must be a Saturday or Sunday");
    }
}

[Weekend] partial record SomeWeekend;
```
## Multiple Attributes
You can apply multiple attributes:
```csharp
[AttributeUsage(AttributeTargets.Class)]
class The90sAttribute : Attribute, IBaseTypeValidation<DateTime>
{
    public void Validate(DateTime value)
    {
        if (value.Year < 1990 || value.Year > 1999)
            throw new ArgumentOutOfRangeException(nameof(value), value, "must be in the 90s");
    }
}

[The90s, Weekend] partial record SomeWeekendInThe90s;
```
---
[![NuGet Package](https://img.shields.io/nuget/v/AndreasDorfer.BaseTypes.Arbitraries.svg)](https://www.nuget.org/packages/AndreasDorfer.BaseTypes.Arbitraries/)
## Arbitraries
Do you use [FsCheck](https://fscheck.github.io/FsCheck/)? Check out `AD.BaseTypes.Arbitraries`.
### NuGet Package
    PM> Install-Package AndreasDorfer.BaseTypes.Arbitraries -Version 0.4.0
### Example
```csharp
[IntRange(Min, Max)]
partial record ZeroToTen
{
    public const int Min = 0, Max = 10;
}

const int MinProduct = ZeroToTen.Min * ZeroToTen.Min;
const int MaxProduct = ZeroToTen.Max * ZeroToTen.Max;

IntRangeArbitrary<ZeroToTen> arb = new(ZeroToTen.Min, ZeroToTen.Max);

Prop.ForAll(arb, arb, (a, b) =>
{
    var product = a * b;
    return product >= MinProduct && product <= MaxProduct;
}).QuickCheckThrowOnFailure();
```
---
[![NuGet Package](https://img.shields.io/nuget/v/AndreasDorfer.BaseTypes.FSharp.svg)](https://www.nuget.org/packages/AndreasDorfer.BaseTypes.FSharp/)
## F#
Do you want to use the generated types in [F#](https://fsharp.org/)? Check out `AD.BaseTypes.FSharp`. The `BaseType` and `BaseTypeResult` modules offer some useful functions.
### NuGet Package
    PM > Install-Package AndreasDorfer.BaseTypes.FSharp -Version 0.4.0
### Example
```fsharp
match (1995, 1, 1) |> DateTime |> BaseType.create<SomeWeekendInThe90s, _> with
| Ok (BaseType.Value dateTime) -> printf "%s" <| dateTime.ToShortDateString()
| Error msg -> printf "%s" msg
```
---
## Options
You can configure the generator to emit the `Microsoft.FSharp.Core.AllowNullLiteral(false)` attribute.

1. Add a reference to [FSharp.Core](https://www.nuget.org/packages/FSharp.Core/).
2. Add the file `AD.BaseTypes.Generator.json` to your project:
```json
{
  "AllowNullLiteral": false
}
```
3. Add the following `ItemGroup` to your project file:
```xml
<ItemGroup>
  <AdditionalFiles Include="AD.BaseTypes.Generator.json" />
</ItemGroup>
```
---
[![NuGet Package](https://img.shields.io/nuget/v/AndreasDorfer.BaseTypes.ModelBinders.svg)](https://www.nuget.org/packages/AndreasDorfer.BaseTypes.ModelBinders/)
## ASP.NET Core
Du you need model binding support for [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/?view=aspnetcore-5.0)? Check out `AD.BaseTypes.ModelBinders`. 
### NuGet Package
    PM> Install-Package AndreasDorfer.BaseTypes.ModelBinders -Version 0.4.0
### Configuration
```csharp
services.AddControllers(options => options.UseBaseTypeModelBinders());
```
---
[![NuGet Package](https://img.shields.io/nuget/v/AndreasDorfer.BaseTypes.OpenApiSchemas.svg)](https://www.nuget.org/packages/AndreasDorfer.BaseTypes.OpenApiSchemas/)
## Swagger
Do you use [Swagger](https://swagger.io/)? Check out `AD.BaseTypes.OpenApiSchemas`.
### NuGetPackage
    PM> Install-Package AndreasDorfer.BaseTypes.OpenApiSchemas -Version 0.4.0
### Configuration
```csharp
services.AddSwaggerGen(c =>
{
    //c.SwaggerDoc(...)
    c.UseBaseTypeSchemas();
});
```
---
## Note
`AD.BaseTypes` is in an early stage.
