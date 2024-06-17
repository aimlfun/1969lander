// C# class created from a translation of Jim Storer's code converted to C by Kristopher Johnson in 2019, then to C# by me.
// Please note: this uses the *correct* maths - there was a bug in Jim's original code according to this clever person:
// See https://martincmartin.com/2024/06/14/how-i-found-a-55-year-old-bug-in-the-first-lunar-lander-game/

namespace LunarLander;
/*
    SUICIDE BURN
   
    Rating: PERFECT LANDING !-(LUCKY)
    Remaining fuel (LB): 653.81 (higher is better)
    Impact velocity (MPH): 0.20 (lower is better)
    Burn amounts: 0, 0, 0, 0, 0, 0, 0, 193.62253477887234, 193.22874292238433, 192.7381306843956, 192.1178845936319, 191.32145579849572, 190.28193470044968, 188.90214485823577, 187.04027885633354, 184.49052111379075
*/

/// <summary>
/// Class to simulate the lander, and its environment.
/// </summary>
internal class LanderInSimulatedEnvironment
{
    /// <summary>
    /// If true, it will learn and output a formula.
    /// If false, it will use the hard-coded formula. Please note for the hard-coded to work, it requires the suicide burn set to 48 (c_minimumAltitudeInMilesToBurn).
    /// </summary>
    internal const bool c_use_ai = true;

    #region AI CONFIG

    // if you set all the inputs to false, this simulation will refuse to run, as it requires inputs to tune the lander

    /// <summary>
    /// Should the altitude be an input to the neural network?
    /// </summary>
    const bool c_altitudeIsNetworkInput = true;

    /// <summary>
    /// Should the downward speed be an input to the neural network?
    /// </summary>
    const bool c_downwardSpeedIsNetworkInput = true;

    /// <summary>
    /// Should the fuel remaining be an input to the neural network?
    /// </summary>
    const bool c_fuelRemainingIsNetworkInput = true;

    /// <summary>
    /// Should the time remaining be an input to the neural network?
    /// </summary>
    const bool c_timeRemainingIsNetworkInput = true;

    /// <summary>
    /// How many hidden neurons in the neural network. This is network with n inputs, n hidden neurons, and 1 output (burn amount).
    /// This defines the hidden layer of the neural network.
    /// </summary>
    const int c_hiddenNeurons = 0;

    /// <summary>
    /// If higher than this, it won't fire the engines. 
    /// Default is 130 miles (i.e. can burn the engines at 130 miles or lower).
    /// Suicide mode is 48. It's aptly named, because you would be crazy to do this in real life. It will cope, but leave zero contigency if you had hardware issues.
    /// </summary>
    const int c_minimumAltitudeInMilesToBurn = 48; // MIN VALUE: 48. It will refuse to run below 48, as a solid 200 max burn will not arrest the lander before it craters.

    /// <summary>
    /// 0 = soft as you can.
    /// >0, we don't reward the AI for doing it better (therefore fuel is the objective)
    /// Whilst we'd love to touch down very gently, or at least the occupants might, if our goal is maximimise fuel we have to sacrifice additional MPH.
    /// </summary>
    const float c_acceptableImpactMPH = 0; // how hard we are ok with crashing

    /// <summary>
    /// Returns the configuration of the AI.
    /// </summary>
    /// <returns></returns>
    internal static string GetConfig()
    {
        return $"AI INPUT: Altitude: {c_altitudeIsNetworkInput}, Downward Speed: {c_downwardSpeedIsNetworkInput}, Fuel Remaining: {c_fuelRemainingIsNetworkInput}, Time Remaining: {c_timeRemainingIsNetworkInput} | # hidden neurons: {c_hiddenNeurons} | Minimum Altitude to Burn: {c_minimumAltitudeInMilesToBurn}";
    }

    /// <summary>
    /// Returns true if configured for suicide burn.
    /// </summary>
    /// <returns></returns>
    internal static bool IsSuicideBurn
    {
        get
        {
            return c_minimumAltitudeInMilesToBurn < 50;
        }
    }

    #endregion

    #region CONSTANTS
    /// <summary>
    /// Gravity for the moon.
    /// </summary>
    const double c_gravity = .001;

    /// <summary>
    /// Empty weight lbs. i.e. without fuel
    /// </summary>
    const double c_weightOfLunarLanderWithoutFuelInLBs = 16500; // LBs

    /// <summary>
    /// Thrust per pound of fuel burned.
    /// </summary>
    const double c_thrustPerPoundOfFuelBurned = 1.8;

    /// <summary>
    /// Weight of the fuel.
    /// </summary>
    const double c_weightOfFullTankOfFuelLBs = 16000;
    #endregion

    /// <summary>
    /// This provides the number of inputs for the neural network based on how many settings are enabled.
    /// </summary>
    readonly static int neuralNetworkInputCount = (c_altitudeIsNetworkInput ? 1 : 0) +
                                                  (c_downwardSpeedIsNetworkInput ? 1 : 0) +
                                                  (c_fuelRemainingIsNetworkInput ? 1 : 0) +
                                                  (c_timeRemainingIsNetworkInput ? 1 : 0);

    /// <summary>
    /// Enable us to provide replacements for "inputs[0]" with the actual names of the inputs.
    /// </summary>
    internal readonly static string[] NamesOfNeuralNetworkInputs;

    /// <summary>
    /// The unique id of the lander.
    /// </summary>
    internal int Id { get; }

    /// <summary>
    /// This contains the neural network that controls the lander.
    /// </summary>
    internal NeuralNetwork Brain { get; private set; }

    /// <summary>
    /// Altitude in miles.
    /// </summary>
    private double _altitudeMiles;

    /// <summary>
    /// Intermediate altitude in miles. Computed during the burn.
    /// </summary>
    private double _intermediateAltitudeMiles;

    /// <summary>
    /// Intermediate velocity in miles per second.
    /// </summary>
    private double _intermediateVelocityMilesPerSec;

    /// <summary>
    /// Fuel burn rate in lbs per second.
    /// </summary>
    private double _fuelRateLBsPerSec;

    /// <summary>
    /// Elapsed time in seconds.
    /// </summary>
    private double _elapsedTimeSec;

    /// <summary>
    /// Total weight in lbs.
    /// </summary>
    private double _totalWeightLBs;

    /// <summary>
    /// Time elapsed in the current 10 second turn.
    /// </summary>
    private double _timeElapsedInCurrent10secTurn;

    /// <summary>
    /// Time remaining in the current 10 second turn.
    /// </summary>
    private double _timeRemainingInCurrent10secTurn;

    /// <summary>
    /// Downward speed in miles per second.
    /// </summary>
    private double _downwardSpeedMilesPerSec;

    /// <summary>
    /// Fuel remaining upon landing.
    /// </summary>
    internal double FuelRemaining
    {
        get
        {
            return _totalWeightLBs - c_weightOfLunarLanderWithoutFuelInLBs;
        }
    }

    /// <summary>
    /// Impact velocity in miles per hour.
    /// </summary>
    internal double ImpactVelocityMPH { get; private set; }

    /// <summary>
    /// Contains the "score" we attribute to the AI's landing (based on how well it landed).
    /// </summary>
    internal int Score { get; private set; } = 0;

    /// <summary>
    /// We need these to output the fuel burn amounts for the user for the best landing.
    /// </summary>
    internal List<double> BurnAmounts { get; private set; } = [];

    /// <summary>
    /// Static constructor. Ensures that the neural network has at least one input. This avoids doing it in the constructor for every lander.
    /// </summary>
    static LanderInSimulatedEnvironment()
    {
        if (neuralNetworkInputCount == 0)
        {
            // no inputs to the neural network. This is a problem. We cannot guide the lander without inputs.
            Console.WriteLine("No inputs to the neural network. Please set at least one input to true.");
            Environment.Exit(-1);
        }

        // sanity check. It isn't possible to go lower. below 47 miles the max thrust of 200 LB will not avoid a crash.
        if (c_minimumAltitudeInMilesToBurn < 48)
        {
            // this code is reachable if you set the minimum altitude to burn to less than 48.
            Console.WriteLine("Minimum altitude to burn fuel must be 48 miles or higher.");
            Console.WriteLine("At 38 miles even with max burn of 200 it will make a crater.");
            Environment.Exit(-1);
        }

        if (c_hiddenNeurons < 0)
        {
            // this code is reachable if you set the hidden neurons to less than 0.
            Console.WriteLine("Hidden neurons must be 0 or higher.");
            Environment.Exit(-1);
        }

        // for the "formula" to work, we need to know the names of the inputs.
        List<string> inputs = [];
        if (c_altitudeIsNetworkInput) inputs.Add("(AltitudeInMiles/150)");
        if (c_downwardSpeedIsNetworkInput) inputs.Add("DownwardSpeedInMilesPerSecond");
        if (c_fuelRemainingIsNetworkInput) inputs.Add("(FuelRemainingLBs/WeightOfFullTankOfFuelLBs)");
        if (c_timeRemainingIsNetworkInput) inputs.Add("(ElapsedTimeInSeconds/200)");

        NamesOfNeuralNetworkInputs = [.. inputs];
    }

    /// <summary>
    /// Constructor. Makes a new lander, with a random neural network brain, and the environment set up for a new landing.
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable. FALSE POSTIVE. AddNewBrain is called in the constructor.
    internal LanderInSimulatedEnvironment(int id)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
        Id = id;

        AddNewBrain();
        ResetEnvironment(); // put the lander in orbit
    }

    /// <summary>
    /// Assign a neural network to the lander that is the size configured in the AI settings.
    /// </summary>
    internal void AddNewBrain()
    {
        // use the hidden neurons if set, otherwise use the input count.
        if (c_hiddenNeurons > 0)
        {
            Brain = new NeuralNetwork(Id, [neuralNetworkInputCount /* inputs */, c_hiddenNeurons, 1 /* burn amount */]);
        }
        else
        {
            // no hidden neurons?! Believe it or not, this still works!
            Brain = new NeuralNetwork(Id, [neuralNetworkInputCount /* inputs */, 1 /* burn amount */]);
        }
    }

    /// <summary>
    /// Reset the lander to be orbiting the moon, ready for descent.
    /// </summary>
    internal void ResetEnvironment()
    {
        _altitudeMiles = 120;
        _downwardSpeedMilesPerSec = 1;

        // calculations like the impact of thrust relies on the full weight of the lander including fuel
        _totalWeightLBs = c_weightOfFullTankOfFuelLBs + c_weightOfLunarLanderWithoutFuelInLBs;

        _elapsedTimeSec = 0;
        ImpactVelocityMPH = 0;
        Score = 0;

        BurnAmounts.Clear();
    }

    /// <summary>
    /// Here's where the magic happens. The lander attempts to land on the moon. This runs in a loop until the lander has landed or run out of fuel.
    /// </summary>
    internal void AttemptLanding()
    {
        while (true)
        {
#if USING_FORMULA_NOT_AI
            _fuelRateLBsPerSec = GetBurnAmountUsingFormula(); // instead of the UI asking for the burn rate, the AI will provide it.
#else            
            _fuelRateLBsPerSec = GetAIBurnAmount(); // instead of the UI asking for the burn rate, the AI will provide it.
#endif 
            // store the amount burned in the current 10 second turn
            if (FuelRemaining > 0.001) BurnAmounts.Add(_fuelRateLBsPerSec);

            _timeRemainingInCurrent10secTurn = 10;

            while (true)
            {
                // no more fuel?
                if (FuelRemaining < .001)
                {
                    FuelOut();
                    return;
                }

                // is the 10 second time up?
                if (_timeRemainingInCurrent10secTurn < .001)
                {
                    break; // prompt for new fuel rate (10 second turn is up)
                }

                _timeElapsedInCurrent10secTurn = _timeRemainingInCurrent10secTurn;

                if (c_weightOfLunarLanderWithoutFuelInLBs + _timeElapsedInCurrent10secTurn * _fuelRateLBsPerSec - _totalWeightLBs > 0)
                {
                    _timeElapsedInCurrent10secTurn = FuelRemaining / _fuelRateLBsPerSec;
                }

                ApplyThrust();

                if (_intermediateAltitudeMiles <= 0)
                {
                    LoopUntilOnTheMoon();
                    return;
                }

                if ((_downwardSpeedMilesPerSec > 0) && (_intermediateVelocityMilesPerSec < 0))
                {
                    while (true)
                    {
                        double tempVar = (1 - _totalWeightLBs * c_gravity / (c_thrustPerPoundOfFuelBurned * _fuelRateLBsPerSec)) / 2;

                        _timeElapsedInCurrent10secTurn = _totalWeightLBs * _downwardSpeedMilesPerSec / (c_thrustPerPoundOfFuelBurned * _fuelRateLBsPerSec * (tempVar + Math.Sqrt(tempVar * tempVar + _downwardSpeedMilesPerSec / (2 * c_thrustPerPoundOfFuelBurned))));

                        ApplyThrust();

                        if (_intermediateAltitudeMiles <= 0)
                        {
                            LoopUntilOnTheMoon();
                            return;
                        }

                        UpdateLanderState();

                        if (-_intermediateVelocityMilesPerSec < 0 || _downwardSpeedMilesPerSec <= 0)
                        {
                            break;
                        }
                    }

                    continue;
                }

                UpdateLanderState();
            }
        }
    }

    /// <summary>
    /// Lander ran out of fuel... Oops, probably in lots of pieces immediately following.
    /// </summary>
    private void FuelOut()
    {
        _timeElapsedInCurrent10secTurn = (Math.Sqrt(_downwardSpeedMilesPerSec * _downwardSpeedMilesPerSec + 2 * _altitudeMiles * c_gravity) - _downwardSpeedMilesPerSec) / c_gravity;
        _downwardSpeedMilesPerSec += c_gravity * _timeElapsedInCurrent10secTurn;
        _elapsedTimeSec += _timeElapsedInCurrent10secTurn;

        ArrivedOnMoon(); // probably in lots of pieces...
    }

    /// <summary>
    /// Player has almost landed, loop until the lander is on the moon.
    /// </summary>
    private void LoopUntilOnTheMoon()
    {
        // the simulation doesn't prompt for burn when under 1 mile in height, it uses the thrust provided at "0".
        while (_altitudeMiles > 0)
        {
            _timeElapsedInCurrent10secTurn = 2 * _altitudeMiles / (_downwardSpeedMilesPerSec + Math.Sqrt(_downwardSpeedMilesPerSec * _downwardSpeedMilesPerSec + 2 * _altitudeMiles * (c_gravity - c_thrustPerPoundOfFuelBurned * _fuelRateLBsPerSec / _totalWeightLBs)));

            ApplyThrust();

            UpdateLanderState();
        }

        ArrivedOnMoon();
    }

    /// <summary>
    /// Adjust the lander state based on the elapsed time and the thrust applied.
    /// </summary>
    private void UpdateLanderState()
    {
        _elapsedTimeSec += _timeElapsedInCurrent10secTurn;
        _timeRemainingInCurrent10secTurn -= _timeElapsedInCurrent10secTurn;

        // weight decreases as we burn fuel
        _totalWeightLBs -= _timeElapsedInCurrent10secTurn * _fuelRateLBsPerSec;

        _altitudeMiles = _intermediateAltitudeMiles;
        _downwardSpeedMilesPerSec = _intermediateVelocityMilesPerSec;
    }

    /// <summary>
    /// Apply the thrust to the lander.
    /// </summary>
    private void ApplyThrust()
    {
        double Q = _timeElapsedInCurrent10secTurn * _fuelRateLBsPerSec / _totalWeightLBs;
        double Q_2 = Math.Pow(Q, 2); // q^2
        double Q_3 = Math.Pow(Q, 3); // q^3
        double Q_4 = Math.Pow(Q, 4); // q^4
        double Q_5 = Math.Pow(Q, 5); // q^5 

        // update the intermediate velocity and altitude. The velocity is a Taylor series expansion.
        _intermediateVelocityMilesPerSec = _downwardSpeedMilesPerSec + c_gravity * _timeElapsedInCurrent10secTurn + c_thrustPerPoundOfFuelBurned * (-Q - Q_2 / 2 - Q_3 / 3 - Q_4 / 4 - Q_5 / 5);
        _intermediateAltitudeMiles = _altitudeMiles - c_gravity * _timeElapsedInCurrent10secTurn * _timeElapsedInCurrent10secTurn / 2 - _downwardSpeedMilesPerSec * _timeElapsedInCurrent10secTurn + c_thrustPerPoundOfFuelBurned * _timeElapsedInCurrent10secTurn * (Q / 2 + Q_2 / 6 + Q_3 / 12 + Q_4 / 20 + Q_5 / 30);
    }

    /// <summary>
    /// I plug in the AI derived formula, and it uses that.
    /// </summary>
    /// <returns></returns>
#pragma warning disable S1144 // Unused private types or members should be removed. FALSE POSITIVE. This code runs if you set c_use_ai = false.
    private double GetBurnAmountUsingFormula()
#pragma warning restore S1144 // Unused private types or members should be removed
    {
        // we trained it using 48 miles, if you change the number you need to retrain and replace the formula.
        if (c_minimumAltitudeInMilesToBurn != 48)
        {
            Console.WriteLine("The formula only works with a minimum altitude to burn of 48 miles (suicide burn).");
            Environment.Exit(-1);
        }

        /*

        I ran the AI for a while, and it came up with this formula. It was a suicide burn, and it managed 0.41mph impact (very good).
               
        Rating: PERFECT LANDING !-(LUCKY)
        Remaining fuel(LB): 631.47(higher is better)
        Impact velocity(MPH): 0.41(lower is better)
        Burn amounts: 0, 0, 0, 0, 0, 0, 0, 197.90470027642212, 196.84244526828476, 195.23664459481427, 192.812675419484, 189.1713501290763, 183.75957532825663, 175.87803562758688, 164.79313455026283, 150.04021541771243

        Formula: 
            _fuelRateLBsPerSec = 200 * (Math.Tanh((0.6117000070225913 * inputs[0]) + (0.8819500360259553 * inputs[1]) + (0.8117000050842762 * inputs[2])+(0.5297500137239695 * inputs[3])+0.48855000972980633))
                                        ^ DERIVED BY AI
        */

        // If we're going for a suicide burn, then don't ask the AI until the required altitude is reached.
        // Of course you could make this part of the training, punishing it for a premature burn, and adding the 
        // min altitude as an input. It'll take longer to train, and probably need more neurons.

        if (_altitudeMiles > c_minimumAltitudeInMilesToBurn) return 0;

        List<double> inputs = [];

        // add the chosen inputs to the neural network "inputs" list
        inputs.Add(_altitudeMiles / 150);
        inputs.Add(_downwardSpeedMilesPerSec);
        inputs.Add(FuelRemaining / c_weightOfFullTankOfFuelLBs);
        inputs.Add(_elapsedTimeSec / 200);

        // ai derived this formula.
        _fuelRateLBsPerSec = 200 * (Math.Tanh((0.6117000070225913 * inputs[0]) + (0.8819500360259553 * inputs[1]) + (0.8117000050842762 * inputs[2]) + (0.5297500137239695 * inputs[3]) + 0.48855000972980633));

        // the thrusters won't even fire without at least a rate of 8, so anything below that is 0. If AI returned minus values, this takes care of them.
        if (_fuelRateLBsPerSec < 8) return 0;

        // the thrusters can't fire at more than 200 lbs per second.
        if (_fuelRateLBsPerSec > 200) return 200;

        // valid values are 0, 8-200
        return _fuelRateLBsPerSec;
    }

    /// <summary>
    /// Ask the neural network how much fuel to burn.
    /// </summary>
    private double GetAIBurnAmount()
    {
        // If we're going for a suicide burn, then don't ask the AI until the required altitude is reached.
        // Of course you could make this part of the training, punishing it for a premature burn, and adding the 
        // min altitude as an input. It'll take longer to train, and probably need more neurons.
        if (_altitudeMiles > c_minimumAltitudeInMilesToBurn) return 0;

        // What data does it require to land without running out of fuel, and not causing the occupants to die?

        // Available Inputs  are: elapsed time, altitude, downward speed, fuel remaining
        //           Outputs are: fuel burn rate

        // The burn is at specific points in time so "time" would be a logical dimension. As it's trying to reduce the downward velocity, it probably needs to know that. It is also designed to minimise fuel usage.
        // There are many questions as to what it really needs. Play with the constants and see how it does.

        List<double> inputs = [];

        // add the chosen inputs to the neural network "inputs" list
        if (c_altitudeIsNetworkInput) inputs.Add(_altitudeMiles / 150);
        if (c_downwardSpeedIsNetworkInput) inputs.Add(_downwardSpeedMilesPerSec);
        if (c_fuelRemainingIsNetworkInput) inputs.Add(FuelRemaining / c_weightOfFullTankOfFuelLBs);
        if (c_timeRemainingIsNetworkInput) inputs.Add(_elapsedTimeSec / 200);

        double[] neuralNetworkInput = [.. inputs];
        double[] outputFromNeuralNetwork = Brain.FeedForward(neuralNetworkInput); // process inputs

        // AI should return 0..1. It might return less than 0 (tanh min = -1), but we don't care - we override that.
        // 0 = don't burn, 1 = full burn (tanh max). Therefore we scale it 0..200
        _fuelRateLBsPerSec = outputFromNeuralNetwork[0] * 200;

        // the thrusters won't even fire without at least a rate of 8, so anything below that is 0. If AI returned minus values, this takes care of them.
        if (_fuelRateLBsPerSec < 8) return 0;

        // the thrusters can't fire at more than 200 lbs per second.
        if (_fuelRateLBsPerSec > 200) return 200;

        // valid values are 0, 8-200
        return _fuelRateLBsPerSec;
    }

    /// <summary>
    /// Arrived on the moon via controlled descent or crash. Calculate the score. I hate to judge, but the AI doesn't mind.
    /// HIGHER SCORE = BETTER LANDING.
    /// We'll then pick the networks with a tendency to get the highest scores, and clone them, and mutate them. Over a short period of time, the AI will get better and better.
    /// </summary>
    private void ArrivedOnMoon() // maybe not in one piece...
    {
        ImpactVelocityMPH = 3600 * _downwardSpeedMilesPerSec;

        // Don't reward it for a smoother landing than the acceptable impact. This enables it to maximise fuel.
        if (ImpactVelocityMPH >= 0 && ImpactVelocityMPH < c_acceptableImpactMPH) ImpactVelocityMPH = c_acceptableImpactMPH;

        double score = (40 - ImpactVelocityMPH) * 100000; // multiplier to stop fuel overriding the mph. 40mph = 0 points, 0mph = 4,000,000 points.                                                              0

        // more points for fuel left. It must never get points for fuel left if it crashes (score<0).  Points are 0-100, ratio of fuel remaining : total fuel
        // we apply a negative bonus, if it crashed with remaining fuel, positive if good attempt
        score += ((score <= 0) ? -1 : 1) * (int)(FuelRemaining / c_weightOfFullTankOfFuelLBs * 100f); //100 points for full tank of fuel left.

        Score = (int)score;
    }

    /// <summary>
    /// Rating per Jim Storer's original, which is based on the impact velocity.
    /// </summary>
    /// <returns>Rating text</returns>
    internal string Rating()
    {
        double impactVelocity = 3600 * _downwardSpeedMilesPerSec;

        if (impactVelocity <= 1)
            return "PERFECT LANDING !-(LUCKY)";
        else if (impactVelocity <= 10)
            return "GOOD LANDING-(COULD BE BETTER)";
        else if (impactVelocity <= 22)
            return "CONGRATULATIONS ON A POOR LANDING";
        else if (impactVelocity <= 40)
            return "CRAFT DAMAGE. GOOD LUCK";
        else if (impactVelocity <= 60)
            return "CRASH LANDING-YOU'VE 5 HRS OXYGEN";

        // fatal crash
        return $"SORRY,BUT THERE WERE NO SURVIVORS-YOU BLEW IT!\nIN FACT YOU BLASTED A NEW LUNAR CRATER {impactVelocity * .277777,8:F2} FT. DEEP";
    }
}