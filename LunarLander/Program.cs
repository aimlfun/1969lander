namespace LunarLander;

// The core logic for the "LunarLander" (simulation) is a translation of <http://www.cs.brandeis.edu/~storer/LunarLander/LunarLander/LunarLanderListing.jpg> by Jim Storer 1969,
// that was ported from FOCAL to C by Kristopher Johnson in 2019.

// I've ported it to c# and added a simple AI to learn how to land the lunar lander.
// - The AI is in LunarLearning.cs.
// - The environment and lander are in LanderInSimulatedEnvironment.cs.

// TRIGGER WARNING :)
// "Program.cs" is a bare-minimum port of the original code.  My ported C# code is an _abomination_, please NEVER use GOTO.
// I wanted to stay true to the original for the manual tests (refactoring could have potentially introduced bugs).
// I promise the AI class is better.

/// <summary>
/// Console application.
/// </summary>
internal static class Program
{
    // Global variables
    //
    // A - Altitude (miles)
    // G - Gravity
    // I - Intermediate altitude (miles)
    // J - Intermediate velocity (miles/sec)
    // K - Fuel rate (lbs/sec)
    // L - Elapsed time (sec)
    // M - Total weight (lbs)
    // N - Empty weight (lbs, Note: M - N is remaining fuel weight)
    // S - Time elapsed in current 10-second turn (sec)
    // T - Time remaining in current 10-second turn (sec)
    // V - Downward speed (miles/sec)
    // W - Temporary working variable
    // Z - Thrust per pound of fuel burned

    static double A, G, I, J, K, L, M, N, S, T, V, W, Z;

    /// <summary>
    /// Main entry point for the Lunar Lander simulation.
    /// </summary>
    static void Main()
    {
        Console.WriteLine("Lunar Lander Simulation");
        Console.WriteLine("-----------------------\n");

        Console.WriteLine("AI revamp of Jim Storer's 1971 PDP-8/I game.");
        Console.WriteLine("  See: http://www.cs.brandeis.edu/~storer/LunarLander/LunarLander/LunarLanderListing.jpg");
        Console.WriteLine("C translation by Kristopher Johnson:");
        Console.WriteLine("  See: https://www.cs.brandeis.edu/~storer/LunarLander/LunarLanderTranslations/LunarLanderJohnsonTranslation-c.txt");
        Console.WriteLine("");
        Console.Write("User play (y/n)? (y=yes, n=AI plays) ");

        if (!AcceptYesOrNo())
        {
            LunarLearning.RunSimulation();
            return;
        }

        // PORT OF FOCAL C -> C -> C# BELOW. MY C->C# PORTED CODE SUCKS, RELYING ON "GOTO". I REMOVED THE GOTO STATEMENTS AND REPLACED THEM WITH
        // FUNCTIONS AND LOOPS IN THE AI LANDER VERSION. I ALSO NAMED THE VARIABLES BETTER.

        Console.WriteLine("CONTROL CALLING LUNAR MODULE. MANUAL CONTROL IS NECESSARY");
        Console.WriteLine("YOU MAY RESET FUEL RATE K EACH 10 SECS TO 0 OR ANY VALUE");
        Console.WriteLine("BETWEEN 8 & 200 LBS/SEC. YOU'VE 16000 LBS FUEL. ESTIMATED");
        Console.WriteLine("FREE FALL IMPACT TIME-120 SECS. CAPSULE WEIGHT-32500 LBS\n\n");

        do
        {
            Console.WriteLine("FIRST RADAR CHECK COMING UP\n\n");
            Console.WriteLine("COMMENCE LANDING PROCEDURE");
            Console.WriteLine("TIME,SECS   ALTITUDE,MILES+FEET   VELOCITY,MPH   FUEL,LBS   FUEL RATE");

            A = 120;
            V = 1;
            M = 32500;
            N = 16500;
            G = .001;
            Z = 1.8;
            L = 0;

        start_turn:
            Console.Write($"{L,7:F0}{Math.Truncate(A),16:F0}{5280 * (A - Math.Truncate(A)),7:F0}{3600 * V,15:F2}{(M - N),12:F1}     ");
            InputK();

            T = 10;

        turn_loop:
            while (true)
            {
                if (M - N < .001)
                    goto fuel_out;

                if (T < .001)
                    goto start_turn;

                S = T;

                if (N + S * K - M > 0)
                    S = (M - N) / K;

                ApplyThrust();

                if (I <= 0)
                    goto loop_until_on_the_moon;

                if ((V > 0) && (J < 0))
                {
                    while (true)
                    {
                        W = (1 - M * G / (Z * K)) / 2;

                        // see https://martincmartin.com/2024/06/14/how-i-found-a-55-year-old-bug-in-the-first-lunar-lander-game/
                        // the original FOCAL code formula is wrong, the _correct_ formula is below, using (2xZ)
                        S = M * V / (Z * K * (W + Math.Sqrt(W * W + V / (2 * Z))));
                        ApplyThrust();

                        if (I <= 0)
                            goto loop_until_on_the_moon;

                        UpdateLanderState();

                        if (-J < 0 || V <= 0)
                            goto turn_loop;
                    }
                }

                UpdateLanderState();
            }

        loop_until_on_the_moon:
            while (A > 0)
            {
                S = 2 * A / (V + Math.Sqrt(V * V + 2 * A * (G - Z * K / M)));
                ApplyThrust();
                UpdateLanderState();
            }
            goto on_the_moon;

        fuel_out:
            Console.WriteLine($"FUEL OUT AT {L,8:F2} SECS");
            S = (Math.Sqrt(V * V + 2 * A * G) - V) / G;
            V += G * S;
            L += S;

        on_the_moon:
            ArrivedOnMoon();

            Console.WriteLine("\n\n\nTRY AGAIN?");

        } while (AcceptYesOrNo());

        Console.WriteLine("CONTROL OUT\n\n");
    }

    /// <summary>
    /// Adjust the lander state based on the elapsed time and the thrust applied.
    /// </summary>
    private static void UpdateLanderState()
    {
        L += S;
        T -= S;
        M -= S * K;
        A = I;
        V = J;
    }

    /// <summary>
    /// Apply the thrust to the lander.
    /// </summary>
    private static void ApplyThrust()
    {
        double Q = S * K / M;
        double Q_2 = Math.Pow(Q, 2);
        double Q_3 = Math.Pow(Q, 3);
        double Q_4 = Math.Pow(Q, 4);
        double Q_5 = Math.Pow(Q, 5);

        J = V + G * S + Z * (-Q - Q_2 / 2 - Q_3 / 3 - Q_4 / 4 - Q_5 / 5);
        I = A - G * S * S / 2 - V * S + Z * S * (Q / 2 + Q_2 / 6 + Q_3 / 12 + Q_4 / 20 + Q_5 / 30);
    }

    /// <summary>
    /// Returns a double value from the console input.
    /// </summary>
    /// <param name="value">The double value read from the console input.</param>
    /// <returns>True if the input was successfully read and parsed as a double; otherwise, false.</returns>
    private static bool AcceptDouble(out double value)
    {
        string? doubleInput = Console.ReadLine();

        if (double.TryParse(doubleInput, out value))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// Console entry of the fuel rate K.
    /// </summary>
    private static void InputK()
    {
        while (true)
        {
            Console.Write("K=:");
            bool is_valid_input = AcceptDouble(out K);

            if (!is_valid_input || K < 0 || ((0 < K) && (K < 8)) || K > 200)
            {
                Console.Write("NOT POSSIBLE");

                for (int x = 1; x <= 51; ++x)
                {
                    Console.Write('.');
                }
            }
            else
            {
                return;
            }
        }
    }

    /// <summary>
    /// Arrived on the moon, print the result. Either a perfect landing, a good landing, a poor landing, a crash landing, or a fatal crash.
    /// </summary>
    private static void ArrivedOnMoon()
    {
        Console.WriteLine($"ON THE MOON AT {L,8:F6} SECS");
        W = 3600 * V;
        Console.WriteLine($"IMPACT VELOCITY OF {W,8:F6} M.P.H.");
        Console.WriteLine($"FUEL LEFT: {(M - N),8:F6} LBS");

        if (W <= 1)
            Console.WriteLine("PERFECT LANDING !-(LUCKY)");
        else if (W <= 10)
            Console.WriteLine("GOOD LANDING-(COULD BE BETTER)");
        else if (W <= 22)
            Console.WriteLine("CONGRATULATIONS ON A POOR LANDING");
        else if (W <= 40)
            Console.WriteLine("CRAFT DAMAGE. GOOD LUCK");
        else if (W <= 60)
            Console.WriteLine("CRASH LANDING-YOU'VE 5 HRS OXYGEN");
        else
        {
            Console.WriteLine("SORRY,BUT THERE WERE NO SURVIVORS-YOU BLEW IT!");
            Console.WriteLine($"IN FACT YOU BLASTED A NEW LUNAR CRATER {W * .277777,8:F2} FT. DEEP\n");
        }
    }

    /// <summary>
    /// Prompts the user to enter "YES" or "NO" and returns true if the user entered "YES"/"Y" and false if the user entered "NO"/"N".
    /// </summary>
    /// <returns></returns>
    private static bool AcceptYesOrNo()
    {
        while (true)
        {
            Console.Write("(ANS. YES OR NO):");

            string? buffer = Console.ReadLine();

            if (buffer is not null)
            {
                switch (buffer.ToLower())
                {
                    case "yes":
                    case "y":
                        return true;

                    case "no":
                    case "n":
                        return false;

                    default:
                        break;
                }
            }
        }
    }
}