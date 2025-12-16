using BenchmarkDotNet.Running;
using MicroServer.Benchmarks;

Console.WriteLine("Starting Benchmarks");

BenchmarkRunner.Run<BinarySerializationBenchmarks>();

Console.WriteLine("Benchmarks complete");