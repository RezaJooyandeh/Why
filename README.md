# Why
Ever wondered why you are receiving a random email from a random distribution list? Let's use graph apis to see the chain of groups that make you "eligible" for that email? 😉

## Build
```
> dotnet build
```

## Usage
```
> cd Why
> dotnet run --tocc "foo@bar.com; Named Foo <aaa@bbb.ccc>; test@ing.com" --token "To obtain a token, go to https://developer.microsoft.com/en-us/graph/graph-explorer and sign-in, then use browser Dev Tools to get a token"
```
