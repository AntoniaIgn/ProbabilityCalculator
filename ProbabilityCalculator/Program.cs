using System.Numerics;
using System.Text;

class Program
{
    static readonly Dictionary<string, string> NatureStates = new()
    {
        {"4,0,0,0,0", "Bad"},
        {"3,1,0,0,0", "Unfavorable"},
        {"2,2,0,0,0", "Neutral"},
        {"2,1,1,0,0", "Favorable"},
        {"1,1,1,1,0", "Bad"}
    };

    static void Main(string[] args)
    {
        Console.WriteLine("=== Azul Probability Analysis ===");
        Console.WriteLine("Bag: 100 tiles (20 blue, 20 red, 20 yellow, 20 black, 20 mint)");
        Console.WriteLine("Drawing 4 tiles at once for the first 9 draws\n");

        // Initial bag state
        var initialBag = new Dictionary<string, int>
        {
            {"Blue", 20},
            {"Red", 20},
            {"Yellow", 20},
            {"Black", 20},
            {"Mint", 20}
        };

        PerformAnalysisFromExcel(initialBag);
    }

    static void PerformAnalysisFromExcel(Dictionary<string, int> initialBag)
    {
        Console.Write("Enter the path to the Excel file: ");
        string filePath = Console.ReadLine();

        if (!File.Exists(filePath))
        {
            Console.WriteLine("File does not exist!");
            return;
        }

        try
        {
            var drawData = ReadDrawDataFromExcel(filePath);
            PerformAnalysisWithData(initialBag, drawData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading file: {ex.Message}");
            Console.WriteLine("Please ensure the file has the correct format:");
            Console.WriteLine("Round, Blue, Red, Yellow, Black, Mint");
        }
    }

    static List<Dictionary<string, int>> ReadDrawDataFromExcel(string filePath)
    {
        var drawData = new List<Dictionary<string, int>>();

        // Reading CSV file (Excel file saved as CSV)
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);

        if (lines.Length < 2)
        {
            throw new Exception("File must contain at least a header row and one data row.");
        }

        // Check header
        var header = lines[0].Split(',');
        var expectedHeaders = new[] { "Round", "Blue", "Yellow", "Red", "Black", "Mint" };

        // Find column indices
        var columnIndices = new Dictionary<string, int>();
        for (int i = 0; i < header.Length; i++)
        {
            var cleanHeader = header[i].Trim().Trim('"');
            if (expectedHeaders.Contains(cleanHeader))
            {
                columnIndices[cleanHeader] = i;
            }
        }

        // Read data
        for (int i = 1; i < lines.Length && drawData.Count < 9; i++)
        {
            var values = lines[i].Split(',');

            var drawnTiles = new Dictionary<string, int>
            {
                {"Blue", ParseInt(values, columnIndices.GetValueOrDefault("Blue", -1))},
                {"Red", ParseInt(values, columnIndices.GetValueOrDefault("Red", -1))},
                {"Yellow", ParseInt(values, columnIndices.GetValueOrDefault("Yellow", -1))},
                {"Black", ParseInt(values, columnIndices.GetValueOrDefault("Black", -1))},
                {"Mint", ParseInt(values, columnIndices.GetValueOrDefault("Mint", -1))}
            };

            // Check if sum is 4
            var total = drawnTiles.Values.Sum();
            if (total != 4)
            {
                Console.WriteLine($"Warning: Round {drawData.Count + 1} has {total} tiles instead of 4!");
            }

            drawData.Add(drawnTiles);
        }

        return drawData;
    }

    static int ParseInt(string[] values, int index)
    {
        if (index < 0 || index >= values.Length) return 0;

        var value = values[index].Trim().Trim('"');
        return int.TryParse(value, out int result) ? result : 0;
    }

    static void PerformAnalysisWithData(Dictionary<string, int> initialBag, List<Dictionary<string, int>> drawData)
    {
        Console.WriteLine("\n=== PROBABILITY ANALYSIS ===");

        var results = new List<(int round, Dictionary<string, int> bagState, Dictionary<string, int> drawnTiles,
            Dictionary<string, double> preProbabilities, string actualState)>();

        var currentBag = new Dictionary<string, int>(initialBag);

        for (int round = 1; round <= Math.Min(9, drawData.Count); round++)
        {
            Console.WriteLine($"\nRound {round}:");
            Console.WriteLine($"Bag state BEFORE draw: {string.Join(", ", currentBag.Select(kv => $"{kv.Key}={kv.Value}"))}");

            // Calculate probabilities BEFORE the draw
            var preProbabilities = CalculateAllStateProbabilities(currentBag, 4);

            Console.WriteLine("Probabilities for states BEFORE draw:");
            foreach (var state in preProbabilities.OrderByDescending(kv => kv.Value))
            {
                Console.WriteLine($"  {state.Key}: {state.Value:P6}");
            }

            // Get actual draw data
            var drawnTiles = drawData[round - 1];

            // Analyze actual draw
            var drawnPattern = GetDrawnPattern(drawnTiles);
            var actualState = ClassifyDrawnTiles(drawnPattern);

            Console.WriteLine($"Actual draw: {string.Join(", ", drawnTiles.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}={kv.Value}"))}");
            Console.WriteLine($"Draw pattern: {drawnPattern}");
            Console.WriteLine($"Actual state: {actualState}");

            // Calculate exact probability for this specific draw
            var exactProbability = CalculateExactProbability(currentBag, drawnTiles, 4);
            Console.WriteLine($"Exact probability for this draw: {exactProbability:P10}");

            // Save result
            results.Add((round, new Dictionary<string, int>(currentBag), drawnTiles, preProbabilities, actualState));

            // Update bag with actual data
            foreach (var color in drawnTiles.Keys)
            {
                currentBag[color] = Math.Max(0, currentBag[color] - drawnTiles[color]);
            }

            Console.WriteLine($"Bag state AFTER draw: {string.Join(", ", currentBag.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }

        SaveDetailedResultsToCSV(results);
    }

    static Dictionary<string, double> CalculateAllStateProbabilities(Dictionary<string, int> bag, int drawCount)
    {
        var stateProbabilities = new Dictionary<string, double>
        {
            {"Bad", 0.0},
            {"Unfavorable", 0.0},
            {"Neutral", 0.0},
            {"Favorable", 0.0}
        };

        var totalTiles = bag.Values.Sum();
        if (totalTiles < drawCount) return stateProbabilities;

        // Generate all possible combinations of 4 tiles
        var combinations = GenerateAllCombinations(bag, drawCount);

        var totalProbability = 0.0;

        foreach (var combination in combinations)
        {
            var probability = CalculateExactProbability(bag, combination, drawCount);
            var pattern = GetDrawnPattern(combination);
            var state = ClassifyDrawnTiles(pattern);

            if (stateProbabilities.ContainsKey(state))
            {
                stateProbabilities[state] += probability;
            }

            totalProbability += probability;
        }

        return stateProbabilities;
    }

    static List<Dictionary<string, int>> GenerateAllCombinations(Dictionary<string, int> bag, int drawCount)
    {
        var colors = bag.Keys.ToList();
        var combinations = new List<Dictionary<string, int>>();

        // Recursive generation of all possible combinations
        GenerateCombinationsRecursive(colors, bag, drawCount, 0, new Dictionary<string, int>(), combinations);

        return combinations;
    }

    static void GenerateCombinationsRecursive(List<string> colors, Dictionary<string, int> bag,
        int remaining, int colorIndex, Dictionary<string, int> current, List<Dictionary<string, int>> results)
    {
        if (remaining == 0)
        {
            results.Add(new Dictionary<string, int>(current));
            return;
        }

        if (colorIndex >= colors.Count) return;

        var color = colors[colorIndex];
        var maxTake = Math.Min(remaining, bag[color]);

        for (int take = 0; take <= maxTake; take++)
        {
            current[color] = take;
            GenerateCombinationsRecursive(colors, bag, remaining - take, colorIndex + 1, current, results);
        }
    }

    static string GetDrawnPattern(Dictionary<string, int> drawn)
    {
        var counts = drawn.Values.Where(v => v > 0).OrderByDescending(v => v);
        return string.Join(",", counts) + string.Concat(Enumerable.Repeat(",0", 5 - counts.Count()));
    }

    static string ClassifyDrawnTiles(string pattern)
    {
        if (NatureStates.ContainsKey(pattern))
        {
            return NatureStates[pattern];
        }
        return "Unknown";
    }

    static double CalculateExactProbability(Dictionary<string, int> bag, Dictionary<string, int> drawn, int totalDraw)
    {
        var totalTiles = bag.Values.Sum();
        if (totalTiles < totalDraw) return 0.0;

        BigInteger numerator = 1;
        foreach (var color in bag.Keys)
        {
            var drawnCount = drawn.ContainsKey(color) ? drawn[color] : 0;
            numerator *= Combination(drawnCount, bag[color]);
        }

        var denominator = Combination(totalDraw, totalTiles);

        if (denominator == 0) return 0.0;

        return (double)numerator / (double)denominator;
    }

    static BigInteger Combination(int k, int n)
    {
        if (k > n || k < 0) return 0;
        if (k == 0 || k == n) return 1;

        BigInteger result = 1;
        for (int i = 1; i <= k; i++)
        {
            result = result * (n - k + i) / i;
        }
        return result;
    }

    static void SaveDetailedResultsToCSV(List<(int round, Dictionary<string, int> bagState, Dictionary<string, int> drawnTiles,
        Dictionary<string, double> preProbabilities, string actualState)> results)
    {
        var filePath = "C:\\Users\\toni\\Desktop\\Statistics-Results.csv";

        try
        {
            var header = "Round,Bag_Blue,Bag_Red,Bag_Yellow,Bag_Black,Bag_Mint," +
                        "Drawn_Blue,Drawn_Red,Drawn_Yellow,Drawn_Black,Drawn_Mint," +
                        "Actual_State,Prob_Bad,Prob_Unfavorable,Prob_Neutral,Prob_Favorable\n";
            File.WriteAllText(filePath, header);

            foreach (var result in results)
            {
                var line = $"{result.round}," +
                          $"{result.bagState["Blue"]},{result.bagState["Red"]},{result.bagState["Yellow"]},{result.bagState["Black"]},{result.bagState["Mint"]}," +
                          $"{result.drawnTiles["Blue"]},{result.drawnTiles["Red"]},{result.drawnTiles["Yellow"]},{result.drawnTiles["Black"]},{result.drawnTiles["Mint"]}," +
                          $"{result.actualState}," +
                          $"{result.preProbabilities.GetValueOrDefault("Bad", 0):F10}," +
                          $"{result.preProbabilities.GetValueOrDefault("Unfavorable", 0):F10}," +
                          $"{result.preProbabilities.GetValueOrDefault("Neutral", 0):F10}," +
                          $"{result.preProbabilities.GetValueOrDefault("Favorable", 0):F10}\n";
                File.AppendAllText(filePath, line);
            }

            Console.WriteLine($"\nDetailed analysis saved to {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving file: {ex.Message}");
        }
    }
}