```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
Intel Core i7-14700K 3.40GHz, 1 CPU, 28 logical and 20 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3


```
| Method                         | Mean      | Error    | StdDev   | Median    | Max       | Min       | P95       | Allocated |
|------------------------------- |----------:|---------:|---------:|----------:|----------:|----------:|----------:|----------:|
| IsEnabled_WithRules_Typed      |  36.72 ns | 0.755 ns | 0.928 ns |  37.33 ns |  37.65 ns |  35.32 ns |  37.60 ns |         - |
| IsEnabled_WithRules_AOT        |  22.49 ns | 0.062 ns | 0.055 ns |  22.51 ns |  22.55 ns |  22.37 ns |  22.55 ns |         - |
| IsEnabled_NoRules_AOT          |  17.34 ns | 0.031 ns | 0.026 ns |  17.34 ns |  17.39 ns |  17.29 ns |  17.38 ns |         - |
| IsEnabled_WithRules_Dictionary |  33.72 ns | 0.125 ns | 0.117 ns |  33.71 ns |  33.89 ns |  33.48 ns |  33.86 ns |         - |
| IsEnabled_Complex_Typed        | 113.15 ns | 0.208 ns | 0.174 ns | 113.14 ns | 113.40 ns | 112.81 ns | 113.39 ns |         - |
| IsEnabled_Complex_AOT          |  81.44 ns | 0.191 ns | 0.160 ns |  81.43 ns |  81.82 ns |  81.28 ns |  81.74 ns |         - |
| IsEnabled_Complex_Dictionary   | 104.29 ns | 1.968 ns | 2.021 ns | 103.69 ns | 108.44 ns | 102.25 ns | 107.44 ns |         - |
| Track_Event                    |  41.58 ns | 0.085 ns | 0.071 ns |  41.58 ns |  41.69 ns |  41.49 ns |  41.69 ns |         - |
| IsEnabled_With10Rules_AOT      | 127.14 ns | 0.710 ns | 0.664 ns | 127.22 ns | 128.12 ns | 125.73 ns | 127.93 ns |         - |
| Track_Event_With10Rules_AOT    |  52.31 ns | 0.047 ns | 0.039 ns |  52.31 ns |  52.36 ns |  52.24 ns |  52.36 ns |         - |
