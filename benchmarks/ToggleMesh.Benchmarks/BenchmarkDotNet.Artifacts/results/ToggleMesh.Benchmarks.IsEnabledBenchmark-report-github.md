```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
Intel Core i7-14700K 3.40GHz, 1 CPU, 28 logical and 20 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3


```
| Method                         | Mean     | Error    | StdDev   | Allocated |
|------------------------------- |---------:|---------:|---------:|----------:|
| IsEnabled_WithRules_Typed      | 52.40 ns | 0.352 ns | 0.329 ns |         - |
| IsEnabled_WithRules_AOT        | 24.68 ns | 0.169 ns | 0.158 ns |         - |
| IsEnabled_WithRules_Dictionary | 49.38 ns | 0.269 ns | 0.252 ns |         - |
| IsEnabled_Complex_Typed        | 98.90 ns | 0.337 ns | 0.315 ns |         - |
| IsEnabled_Complex_AOT          | 77.42 ns | 0.295 ns | 0.261 ns |         - |
| IsEnabled_Complex_Dictionary   | 98.79 ns | 0.397 ns | 0.371 ns |         - |
| Track_Event                    | 46.37 ns | 0.136 ns | 0.127 ns |         - |
