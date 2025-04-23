﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;
using System.Text.Json;
using ScottPlot;
using ScottPlot.Palettes;

namespace cli_life
{
    public class Cell
    {
        public bool IsAlive;
        public readonly List<Cell> neighbors = new List<Cell>();
        public bool IsAliveNext;
        public void DetermineNextLiveState()
        {
            int liveNeighbors = neighbors.Where(x => x.IsAlive).Count();
            if (IsAlive)
                IsAliveNext = liveNeighbors == 2 || liveNeighbors == 3;
            else
                IsAliveNext = liveNeighbors == 3;
        }
        public void Advance()
        {
            IsAlive = IsAliveNext;
        }
    }
    public class Board
    {
        public readonly Cell[,] Cells;
        public readonly int CellSize;

        public int Columns { get { return Cells.GetLength(0); } }
        public int Rows { get { return Cells.GetLength(1); } }
        public int Width { get { return Columns * CellSize; } }
        public int Height { get { return Rows * CellSize; } }

        public Board(int width, int height, int cellSize, double liveDensity = .1)
        {
            CellSize = cellSize;

            Cells = new Cell[width / cellSize, height / cellSize];
            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                    Cells[x, y] = new Cell();

            ConnectNeighbors();
            Randomize(liveDensity);
        }

        readonly Random rand = new Random();
        public void Randomize(double liveDensity)
        {
            foreach (var cell in Cells)
                cell.IsAlive = rand.NextDouble() < liveDensity;
        }

        public void Advance()
        {
            foreach (var cell in Cells)
                cell.DetermineNextLiveState();
            foreach (var cell in Cells)
                cell.Advance();
        }
        private void ConnectNeighbors()
        {
            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    int xL = (x > 0) ? x - 1 : Columns - 1;
                    int xR = (x < Columns - 1) ? x + 1 : 0;

                    int yT = (y > 0) ? y - 1 : Rows - 1;
                    int yB = (y < Rows - 1) ? y + 1 : 0;

                    Cells[x, y].neighbors.Add(Cells[xL, yT]);
                    Cells[x, y].neighbors.Add(Cells[x, yT]);
                    Cells[x, y].neighbors.Add(Cells[xR, yT]);
                    Cells[x, y].neighbors.Add(Cells[xL, y]);
                    Cells[x, y].neighbors.Add(Cells[xR, y]);
                    Cells[x, y].neighbors.Add(Cells[xL, yB]);
                    Cells[x, y].neighbors.Add(Cells[x, yB]);
                    Cells[x, y].neighbors.Add(Cells[xR, yB]);
                }
            }
        }
        public void SaveToFile(string fileName)
        {
            if (File.Exists(fileName))
            {
                using (StreamWriter writer = new StreamWriter(fileName))
                {
                    writer.WriteLine($"{Columns} {Rows} {CellSize}");
                    for (int y = 0; y < Rows; y++)
                    {
                        for (int x = 0; x < Columns; x++)
                        {
                            writer.Write(Cells[x, y].IsAlive ? '1' : '0');
                        }
                        writer.WriteLine();
                    }
                }
            }
            else
            {
                throw new Exception($"File {fileName} not found");
            }
        }
        public void LoadFromFile(string fileName)
        {
            if (File.Exists(fileName))
            {
                using (StreamReader reader = new StreamReader(fileName))
                {
                    var dimensions = reader.ReadLine().Split(' ');
                    int cols = int.Parse(dimensions[0]);
                    int rows = int.Parse(dimensions[1]);

                    for (int y = 0; y < ((rows <= Rows) ? rows : Rows); y++)
                    {
                        string line = reader.ReadLine();
                        for (int x = 0; x < ((cols <= Columns) ? cols : Columns); x++)
                        {
                            Cells[x, y].IsAlive = line[x] == '1';
                        }
                    }
                }
            }
            else
            {
                throw new Exception($"File {fileName} not found");
            }
        }
        public void LoadPattern(string fileName, int offsetX = 0, int offsetY = 0)
        {
            if (File.Exists(fileName))
            {
                string[] lines = File.ReadAllLines(fileName);
                for (int y = 0; y < ((lines.Length <= Rows) ? lines.Length : Rows); y++)
                {
                    for (int x = 0; x < ((lines[y].Length <= Columns) ? lines[y].Length : Columns); x++)
                    {
                        int targetX = (x + offsetX) % Columns;
                        int targetY = (y + offsetY) % Rows;
                        Cells[targetX, targetY].IsAlive = lines[y][x] == '1';
                    }
                }
            }
            else
            {
                throw new Exception($"File {fileName} not found");
            }
        }
    }
    public class GameSettings
    {
        public int Width { get; set; } = 50;
        public int Height { get; set; } = 20;
        public int CellSize { get; set; } = 1;
        public double LiveDensity { get; set; } = 0.5;
        public int Delay { get; set; } = 1000;
    }
    public class ClusterAnalyzer
    {
        public static List<HashSet<(int, int)>> FindClusters(Board board)
        {
            var clusters = new List<HashSet<(int, int)>>();
            var visited = new bool[board.Columns, board.Rows];

            for (int y = 0; y < board.Rows; y++)
            {
                for (int x = 0; x < board.Columns; x++)
                {
                    if (board.Cells[x, y].IsAlive && !visited[x, y])
                    {
                        var cluster = new HashSet<(int, int)>();
                        ExploreCluster(board, x, y, visited, cluster);
                        clusters.Add(cluster);
                    }
                }
            }

            return clusters;
        }

        private static void ExploreCluster(Board board, int x, int y, bool[,] visited, HashSet<(int, int)> cluster)
        {
            var queue = new Queue<(int, int)>();
            queue.Enqueue((x, y));
            visited[x, y] = true;

            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                cluster.Add((cx, cy));

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0)
                            continue;

                        int nx = (cx + dx + board.Columns) % board.Columns;
                        int ny = (cy + dy + board.Rows) % board.Rows;

                        if (board.Cells[nx, ny].IsAlive && !visited[nx, ny])
                        {
                            visited[nx, ny] = true;
                            queue.Enqueue((nx, ny));
                        }
                    }
                }
            }
        }

        public static string ClassifyCluster(HashSet<(int x, int y)> cluster, string patternsDir)
        {
            var normalized = NormalizeCluster(cluster);

            var templates = LoadTemplates(patternsDir);

            foreach (var (name, template) in templates)
            {
                if (AreClustersEqual(normalized, template))
                {
                    return name;
                }
            }

            return $"Unknown ({cluster.Count} cells)";
        }

        private static HashSet<(int x, int y)> NormalizeCluster(HashSet<(int x, int y)> cluster)
        {
            int minX = cluster.Min(p => p.x);
            int minY = cluster.Min(p => p.y);

            return [.. cluster.Select(p => (p.x - minX, p.y - minY))];
        }

        private static Dictionary<string, HashSet<(int x, int y)>> LoadTemplates(string dir)
        {
            var templates = new Dictionary<string, HashSet<(int x, int y)>>();

            try
            {
                foreach (var file in Directory.GetFiles(dir, "*.txt"))
                {
                    var pattern = new HashSet<(int x, int y)>();
                    string[] lines = File.ReadAllLines(file);

                    for (int y = 0; y < lines.Length; y++)
                    {
                        for (int x = 0; x < lines[y].Length; x++)
                        {
                            if (lines[y][x] == '1')
                            {
                                pattern.Add((x, y));
                            }
                        }
                    }

                    templates.Add(Path.GetFileNameWithoutExtension(file), pattern);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return templates;
        }

        private static bool AreClustersEqual(
            HashSet<(int x, int y)> cluster1,
            HashSet<(int x, int y)> cluster2)
        {
            if (cluster1.Count != cluster2.Count)
                return false;

            for (int rotation = 0; rotation < 4; rotation++)
            {
                var rotated = RotateCluster(cluster1, rotation);
                if (rotated.SetEquals(cluster2))
                    return true;
            }

            return false;
        }

        private static HashSet<(int x, int y)> RotateCluster(
            HashSet<(int x, int y)> cluster,
            int rotations)
        {
            var result = new HashSet<(int x, int y)>();
            int size = cluster.Max(p => Math.Max(p.x, p.y)) + 1;

            foreach (var (x, y) in cluster)
            {
                var (rx, ry) = (x, y);

                for (int i = 0; i < rotations; i++)
                {
                    (rx, ry) = (ry, size - 1 - rx);
                }

                result.Add((rx, ry));
            }

            return result;
        }
    }
    public class StabilityAnalyzer
    {
        private const int StabilityThreshold = 5;
        private Queue<int> history = new Queue<int>();

        public bool CheckStability(Board board)
        {
            int aliveCount = 0;
            for (int y = 0; y < board.Rows; y++)
                for (int x = 0; x < board.Columns; x++)
                    if (board.Cells[x, y].IsAlive)
                        aliveCount++;

            history.Enqueue(aliveCount);
            if (history.Count > StabilityThreshold)
                history.Dequeue();

            return history.Distinct().Count() == 1 && history.Count == StabilityThreshold;
        }

        public void SaveValue(int generation, string fileName)
        {
            if (File.Exists(fileName))
            {
                List<string> lines = File.ReadAllLines(fileName).ToList();
                string newRecord = $"{DateTime.Now.ToString()}: {generation}";
                if (lines.Count > 0)
                    lines[lines.Count - 1] = newRecord;
                else
                    lines.Add(newRecord);
                int average = 0;
                foreach (string line in lines)
                {
                    average += int.Parse(line.Split(": ")[1]);
                }
                average /= lines.Count;
                lines.Add($"Среднее число поколений: {average}");
                File.WriteAllLines(fileName, lines);
            }
            else
            {
                throw new Exception($"File {fileName} not found");
            }
        }
    }

    public class Statistics
    {
        public static void CalcAndGet()
        {
            string projDir = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
            string dataPath = Path.Combine(projDir, "data.txt");
            string allDataPath = Path.Combine(projDir, "dataAll.txt");
            string plotPath = Path.Combine(projDir, "plot.png");
            string allPlotPath = Path.Combine(projDir, "plotAll.png");

            RunPopulationAnalysis(dataPath);
            CreatePlot(dataPath, plotPath);

            RunPopulationAnalysisAll(allDataPath);
            PlotForAll(allDataPath, allPlotPath);
        }
        
        private static void RunPopulationAnalysis(string fileName)
        {
            double density = 0.4;
            int maxGenerations = 600;
            StabilityAnalyzer stabilityAnalyzer = new StabilityAnalyzer();

            File.WriteAllText(fileName,
                "Поколение Живых_клеток\n");

            var board = new Board(50, 20, 1, density);
            int gen = 0;
            while (gen < maxGenerations && !stabilityAnalyzer.CheckStability(board))
            {
                int aliveCount = 0;
                for (int y = 0; y < board.Rows; y++)
                    for (int x = 0; x < board.Columns; x++)
                        if (board.Cells[x, y].IsAlive)
                            aliveCount++;

                File.AppendAllText(fileName,
                    $"{gen} {aliveCount}\n");

                board.Advance();
                gen++;
            }
        }

        private static void RunPopulationAnalysisAll(string fileName)
        {
            double[] densities = { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9 };
            int maxGenerations = 600;

            File.WriteAllText(fileName,
                "Поколение Плотность Живых_клеток\n");

            foreach (var density in densities)
            {
                var board = new Board(50, 20, 1, density);

                for (int gen = 0; gen < maxGenerations; gen++)
                {
                    int aliveCount = 0;
                    for (int y = 0; y < board.Rows; y++)
                        for (int x = 0; x < board.Columns; x++)
                            if (board.Cells[x, y].IsAlive)
                                aliveCount++;

                    File.AppendAllText(fileName,
                        $"{gen} {density} {aliveCount}\n");

                    board.Advance();
                }
            }
        }

        public static void CreatePlot(string dataFileName, string plotFileName)
        {
            var data = File.ReadAllLines(dataFileName)
                .Skip(1)
                .Select(line => line.Split(' '))
                .Where(parts => parts.Length == 2)
                .Select(parts => new
                {
                    Generation = int.Parse(parts[0]),
                    AliveCells = int.Parse(parts[1])
                })
                .ToList();

            var plot = new Plot();

            var xValues = data.Select(x => (double)x.Generation).ToArray();
            var yValues = data.Select(x => (double)x.AliveCells).ToArray();
            var sig = plot.Add.Scatter(xValues, yValues);
            sig.Color = Colors.Red;
            sig.MarkerSize = 1;

            plot.Title("Динамика числа живых клеток плотность 0.4", size: 16);
            plot.XLabel("Номер поколения", size: 14);
            plot.YLabel("Число клеток", size: 14);

            plot.Axes.AutoScale();

            plot.SavePng(plotFileName, 500, 500);
        }
        
        public static void PlotForAll(string dataFileName, string plotFileName)
        {
            var data = File.ReadAllLines(dataFileName)
                .Skip(1)
                .Select(line => line.Split(' '))
                .Where(parts => parts.Length == 3)
                .Select(parts => new
                {
                    Generation = int.Parse(parts[0]),
                    Density = double.Parse(parts[1]),
                    AliveCells = int.Parse(parts[2])
                })
                .ToList();

            var plot = new Plot();

            var groups = data.GroupBy(x => x.Density);

            var colors = new[] {
        Colors.Red,
        Colors.Orange,
        Colors.Yellow,
        Colors.Green,
        Colors.SkyBlue,
        Colors.DarkBlue,
        Colors.Violet,
        Colors.Gray,
        Colors.Black
      };

            int colorIndex = 0;
            foreach (var group in groups)
            {
                var xValues = group.Select(x => (double)x.Generation).ToArray();
                var yValues = group.Select(x => (double)x.AliveCells).ToArray();

                var sig = plot.Add.Scatter(xValues, yValues);
                sig.Color = colors[colorIndex];
                sig.MarkerSize = 1;
                sig.LegendText = group.First().Density.ToString();

                colorIndex = (colorIndex + 1) % colors.Length;
            }

            plot.Title("Динамика числа живых клеток", size: 16);
            plot.XLabel("Номер поколения", size: 14);
            plot.YLabel("Число клеток", size: 14);

            plot.Axes.AutoScale();
            plot.ShowLegend();

            plot.SavePng(plotFileName, 2000, 1000);
        }

        public static void CalcAvgStability()
        {
            StabilityAnalyzer analyzer = new StabilityAnalyzer();
            string projPath = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
            string statPath = Path.Combine(projPath, "avg_stability/");
            string patterns = Path.Combine(projPath, "patterns/");
            double[] densities = { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9 };
            foreach (double dens in densities)
            {
                string filename = Path.Combine(statPath, "0_" + ((int)(dens * 10) % 10).ToString() + ".txt");
                var f = File.Create(filename);
                f.Close();
                for (int i = 0; i < 15; ++i)
                {
                    Board board = new Board(20, 20, 1, dens);
                    int gen = 0;
                    while (true)
                    {
                        if (analyzer.CheckStability(board))
                        {
                            analyzer.SaveValue(gen, filename);
                            break;
                        }
                        else
                            gen++;
                        board.Advance();
                    }

                }

                Console.WriteLine("Посчитана статистика для плотности:" + dens.ToString());
            }
        }
    }

    class Program
    {
        static Board board;
        static GameSettings settings;
        static StabilityAnalyzer stabilityAnalyzer = new StabilityAnalyzer();
        static int delay;
        static int generation = 1;
        static int stableGeneration = 1;

        static private void Reset(string fileName = "")
        {
            generation = 1;
            stableGeneration = 1;
            board = new Board(
                width: settings.Width,
                height: settings.Height,
                cellSize: settings.CellSize,
                liveDensity: settings.LiveDensity);
            if (!string.IsNullOrEmpty(fileName))
            {
                board.LoadFromFile(fileName);
            }
        }
        static void Render()
        {
            for (int row = 0; row < board.Rows; row++)
            {
                for (int col = 0; col < board.Columns; col++)
                {
                    var cell = board.Cells[col, row];
                    if (cell.IsAlive)
                    {
                        Console.Write('*');
                    }
                    else
                    {
                        Console.Write(' ');
                    }
                }
                Console.Write('\n');
            }
            Console.Write($"Поколение {generation}");
            generation++;
        }

        static void LoadSettings(string fileName)
        {
            try
            {
                string json = File.ReadAllText(fileName);
                settings = JsonSerializer.Deserialize<GameSettings>(json);
                delay = settings.Delay;
            }
            catch
            {
                settings = new GameSettings();
                delay = settings.Delay;
            }
        }

        static int KeyAction(string savePath)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.S)
                {
                    board.SaveToFile(savePath);
                    Console.WriteLine("\nСохранено в board.txt");
                }
                else if (key == ConsoleKey.L)
                {
                    board.LoadFromFile(savePath);
                    Console.WriteLine("\nЗагружено из board.txt");
                }
                else if (key == ConsoleKey.Escape)
                {
                    return 1;
                }
            }
            return 0;
        }
        static void PrintClustersInfo(string patternsPath)
        {
            var clusters = ClusterAnalyzer.FindClusters(board);
            Console.WriteLine($"\nЧисло кластеров: {clusters.Count}");
            foreach (var cluster in clusters.OrderBy(c => -c.Count))
            {
                Console.WriteLine($"{ClusterAnalyzer.ClassifyCluster(cluster, patternsPath)} (size: {cluster.Count})");
            }
        }

        static void RunConsole()
        {
            string projDir = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
            string savePath = Path.Combine(projDir, "board.txt");
            string patternsPath = Path.Combine(projDir, "patterns/");
            LoadSettings(Path.Combine(projDir, "config.json"));
            string analiticsPath = Path.Combine(projDir, "avg_stability/0_1.txt");
            Reset(savePath);
            Reset();
            board.LoadPattern(patternsPath + "blinker.txt");

            while (true)
            {
                if (generation == 10)
                    board.LoadFromFile(savePath);
                if (KeyAction(savePath) == 1)
                    break;

                Console.Clear();
                Render();

                if (stabilityAnalyzer.CheckStability(board))
                {
                    Console.WriteLine($"\nНайдено стационарное состояние: {stableGeneration}");
                    PrintClustersInfo(patternsPath);
                    break;
                }
                else
                    stableGeneration++;


                board.Advance();
                Thread.Sleep(delay);
            }
        }

        static void Main(string[] args)
        {
            //Statistics.CalcAndGet();
            //Statistics.CalcAvgStability();
            RunConsole();
        }
    }
}