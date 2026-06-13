```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
Intel Core i7-14700K 3.40GHz, 1 CPU, 28 logical and 20 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v3


```
| Method                         | Mean     | Error    | StdDev   | Allocated |
|------------------------------- |---------:|---------:|---------:|----------:|
| IsEnabled_WithRules_Typed      | 46.81 ns | 0.327 ns | 0.306 ns |         - |
| IsEnabled_WithRules_Dictionary | 37.24 ns | 0.255 ns | 0.238 ns |         - |
