namespace LunarLander
{
    /// <summary>
    /// A class to teach the landers how to land.
    /// The landers are controlled by a neural network, and the physics of the lander are simulated based of the 1969 Jim Storer Lunar Lander simulation 
    /// (corrected maths, per martincmartin https://martincmartin.com/2024/06/14/how-i-found-a-55-year-old-bug-in-the-first-lunar-lander-game/).
    /// </summary>
    internal static class LunarLearning
    {
        /// <summary>
        /// How many random landers we throw in during the mutate phase, as a percentage of the total number of landers.
        /// </summary>
        private const int c_percentageRandomDuringMutate = 10; // IF YOU USE 75% IT SEEMS REALLY SLOW TO LEARN

        /// <summary>
        /// The number of parallel landers we run during the simulation. It runs in parallel to improve performance, so this can be set quite high (at least on my i7!).
        /// </summary>
        private const int c_numberOfLanders = 5000;

        /// <summary>
        /// The number generations we have run the simulation for (so far).
        /// </summary>
        private static int s_numberOfGenerations = 0;

        /// <summary>
        /// We use this to determine when to call out a better score.
        /// </summary>
        private static int s_lastScore = -int.MaxValue;

        /// <summary>
        /// The "id" of the best performing lander.
        /// </summary>
        private static int s_bestLanderIndex = -1;

        /// <summary>
        /// The lander + environment, keyed by their id.
        /// </summary>
        private static Dictionary<int, LanderInSimulatedEnvironment> s_landers = [];

        /// <summary>
        /// Creates the landers, each one has it's own neural network.
        /// </summary>
        private static void CreateLanders()
        {
            s_landers.Clear();

            // create the required number of landers + heir simulated environment

            for (int i = 0; i < c_numberOfLanders; i++)
            {
                s_landers.Add(i, new LanderInSimulatedEnvironment(i));
            }
        }

        /// <summary>
        /// This puts the lunar landers back to their starting position, in orbit ready for descent.
        /// </summary>
        private static void ResetLanders()
        {
            foreach (int landerIndex in s_landers.Keys)
            {
                s_landers[landerIndex].ResetEnvironment();
            }
        }

        /// <summary>
        /// Mutates the landers. i.e. creates a new generation of landers based off the best performing landers.
        /// </summary>
        private static void MutateLanders()
        {
            // sort them in order of score (ascending). Highest score = best. So we replace top half with a clone the bottom half mutated.
            s_landers = s_landers.OrderBy(x => x.Value.Score).ToDictionary(x => x.Key, x => x.Value);

            LanderInSimulatedEnvironment[] arrayOfLanders = [.. s_landers.Values];

            // the best performing lander is the last one in the array
            s_bestLanderIndex = arrayOfLanders[c_numberOfLanders - 1].Id;

            // replace the 50% worse offenders with the best, then mutate them.
            // we do this by overwriting the top half (lowest fitness) with bottom half.
            for (int worstNeuralNetworkIndex = 0; worstNeuralNetworkIndex < c_numberOfLanders / 2; worstNeuralNetworkIndex++)
            {
                // 50..100 (in 100 neural networks) are in the top performing
                int neuralNetworkToCloneFromIndex = worstNeuralNetworkIndex + c_numberOfLanders / 2; // +50% -> top 50% 

                // replace the worst with the best
                NeuralNetwork.CopyFromTo(arrayOfLanders[neuralNetworkToCloneFromIndex].Brain, arrayOfLanders[worstNeuralNetworkIndex].Brain);

                arrayOfLanders[worstNeuralNetworkIndex].Brain.Mutate(25, 0.5F); // mutate
            }

            // throw in one random ones, just to keep things interesting. It could be a random one performs better than the best.
            // There is probably a break even point depending on size of population. For example it might get to a point where the best is so good that it's not worth mutating it, it also might work out better to keep 1 best,
            // and mutate a few clones but keep 75% random. It's all about finding the right balance.

            int numberOfRandom = c_numberOfLanders * c_percentageRandomDuringMutate / 100;

#pragma warning disable S2583 // Conditionally executed code should be reachable. FALSE POSITIVE. This is reachable, it's just that the number of random can be 0.
            if (numberOfRandom == 0)
            {
                arrayOfLanders[0].AddNewBrain(); // if we have no random, just make one random
            }
            else
            {
                for (int i = 0; i < numberOfRandom; i++)
                {
                    arrayOfLanders[i].AddNewBrain();
                }
            }
#pragma warning restore S2583 // Conditionally executed code should be reachable
        }

        /// <summary>
        /// Runs the simulation: creating landers, running them, mutating them, and repeating. Eventually it should come up with a lander that can land, and then eventually land perfectly.
        /// </summary>
        internal static void RunSimulation()
        {
            Console.WriteLine("");
            Console.WriteLine("Using AI to determine burn rates");

            // change config in LanderInSimulatedEnvironment
            Console.WriteLine(LanderInSimulatedEnvironment.GetConfig());

            if (LanderInSimulatedEnvironment.IsSuicideBurn)
            {
                // change LanderInSimulatedEnvironment.c_minimumAltitudeInMilesToBurn to 130 for normal
                Console.WriteLine("*** Altitude is set to SUICIDE BURN (extremely low before attempting to slow down) ***");
            }

            Console.WriteLine("");

            Console.WriteLine($"Manufacturing {c_numberOfLanders} lunar lander(s)...");
            CreateLanders(); // gives us some landers to work with

            Console.WriteLine("Running Simulation... ctrl-c to end");

            Console.TreatControlCAsInput = true; // stop ctrl-c from killing the app without clean shutdown

            while (true)
            {
                // check to see if user has pressed ctrl-c
                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo key = Console.ReadKey(true);

                    if (key.Key == ConsoleKey.C && key.Modifiers == ConsoleModifiers.Control)
                    {
                        Console.WriteLine("** User requested termination of simulation **");
                        break;
                    }
                }

                s_numberOfGenerations++;

                // run the simulation for each lander in parallel to improve performance.
                Parallel.ForEach(s_landers.Values, lander =>
                {
                    lander.AttemptLanding();
                });

                // all landers crashed or landed. Time to mutate all but the best and try again.
                MutateLanders();

                // output some stats to the console. We do it only when the score improves - to keep the console from getting too cluttered, and to keep the performance up.
                if (s_bestLanderIndex > -1 && s_lastScore != s_landers[s_bestLanderIndex].Score)
                {
                    s_lastScore = s_landers[s_bestLanderIndex].Score;

                    Console.WriteLine($"Epoch: {s_numberOfGenerations} | Score: {s_landers[s_bestLanderIndex].Score} (higher is better) | Rating: " + s_landers[s_bestLanderIndex].Rating()); // label it based on Jim's rating system

                    // our goal is maximise fuel, and minimise impact velocity.
                    Console.WriteLine("Remaining fuel (LB): " + s_landers[s_bestLanderIndex].FuelRemaining.ToString("0.00") + " (higher is better)");
                    Console.WriteLine("Impact velocity (MPH): " + s_landers[s_bestLanderIndex].ImpactVelocityMPH.ToString("0.00") + " (lower is better)");

                    // these are what you would type into the console to get the lander to land.
                    Console.WriteLine("Burn amounts: " + string.Join(", ", s_landers[s_bestLanderIndex].BurnAmounts));
                    OutputFormulaForFuelRate();
                    Console.WriteLine("");
                }

                ResetLanders();
            }
        }

        /// <summary>
        /// Outputs the underlying formula for fuel-rate.
        /// </summary>
        private static void OutputFormulaForFuelRate()
        {
            // Ask the neural network for the formula it uses to determine the burn rate.
            string formula = s_landers[s_bestLanderIndex].Brain.Formula();

            // replace the input[] with the actual names of the inputs to make it more readable.
            for (int i = 0; i < LanderInSimulatedEnvironment.NamesOfNeuralNetworkInputs.Length; i++)
            {
                formula = formula.Replace("input[" + i + "]", LanderInSimulatedEnvironment.NamesOfNeuralNetworkInputs[i]);
            }

            Console.WriteLine(formula);
        }
    }
}