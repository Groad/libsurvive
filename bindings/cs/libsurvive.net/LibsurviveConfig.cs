using System;
using System.Collections.Generic;
using System.Text;

public class LibsurviveConfig {

    public string configFile;                   // File path to config text file (contains calibration, etc.)

    public string playbackFile;                 // File path to playback recording 
    public int playbackFactor;                  // Playback speed of playbackFile (0 = fastest, 1 = real-time)

    public string requiredCalibrationTrackers;  // Select which trackers calibration routine will use
    public BoolConfig allTrackerCalibration;    // Allow all trackers to be used for calibration 

    public int lighthouseCount;                 // Desired number of lighthouses for pose generation
    public int disableLighthouse;               // Disables lighthouse of specified index

    public Disambiguator disambiguator;         // Accepts lightcap elements: processes OOTX, sweep times/angles of pulses.

    public Poser poser;                         // Default poser algorithm
    public Poser configPoser;                   // Poser used to generate calibration
    public Poser seedPoser;                     // Poser used to generate seed-poses (when needed)

    public BoolConfig forceCalibrate;           // Force a new calibration routine 
    public BoolConfig disableCalibrate;         // Disables calibration
    public BoolConfig useIMU;                   // Whether IMU is used in pose generation
    public BoolConfig useKalman;                // Whether Kalman filter is used in pose generation
    public BoolConfig useJacobian;              // Whether Jacobian function is used in pose generation
    public BoolConfig reportPosesInIMUSpace;    // Should poses be reported in the IMU's transform
    public BoolConfig ootxIgnoreSyncError;      // Can we ignore OOTX sync errors

    public static LibsurviveConfig Default = new LibsurviveConfig {
        poser = Poser.SBA,
        forceCalibrate = BoolConfig.Yes,
        useIMU = BoolConfig.No,
    };

    public enum Disambiguator {
        Default = 0,
        Charles,        // Fast, but slightly buggy
        Turvey,         // More complicated, more robust
        StateBased,     // Fast, experimental. Made for tracking very close to lighthouse
    }

    public enum Poser {
        Default = 0,
        Dummy,          // Template
        CharlesSlow,    // Very slow but exhaustive poser (calibration)
        CharlesRefine,
        DaveOrtho,      // Very fast, uses orthographic and affine transforms 
        EPNP,           // Efficient Perspective-n-Point
        SBA,            // Sparse Bundle Adjustment (requires seed poser)
        MPFIT,          // Levenberg-Marquardt least-squares (requires seed poser)
        TurveyTori,     // Fairly high precision, need powerful computer
        OctavioRadii,   // Potentially very fast (incomplete)
    }

    // Bool with option for default (do not specify default settings on startup)
    public enum BoolConfig {
        Default = 0,
        No,
        Yes,
    }

    // Constructor for object initialization
    public LibsurviveConfig() { }

    // Creates parameter array for initialization of libsurvive
    public string[] GetParameterStrings() {

        List<string> args = new List<string>() { "unity", "--v" };

        if (!string.IsNullOrEmpty(playbackFile)) {
            args.AddRange(new[] { "--playback", playbackFile });
            args.AddRange(new[] { "--playback-factor", playbackFactor.ToString() });
        }
        if (disambiguator != Disambiguator.Default) {
            args.AddRange(new[] { "--disambiguator", Enum.GetName(typeof(Disambiguator), disambiguator) });
        }
        if (forceCalibrate != BoolConfig.Default) {
            args.AddRange(new[] { "--force-calibrate", forceCalibrate == BoolConfig.Yes ? "1" : "0" });
        }
        if (disableCalibrate != BoolConfig.Default && forceCalibrate != BoolConfig.Yes) {
            args.Add("--disable-calibrate");
        }
        if (useKalman != BoolConfig.Default) {
            args.AddRange(new[] { "--use-kalman", useKalman == BoolConfig.Yes ? "1" : "0" });
        }
        if (useJacobian != BoolConfig.Default) {
            args.AddRange(new[] { "--use-jacobian-function", useJacobian == BoolConfig.Yes ? "1" : "0" });
        }
        if (useIMU != BoolConfig.Default) {
            args.AddRange(new[] { "--use-imu", useIMU == BoolConfig.Yes ? "1" : "0" });
        }
        if (reportPosesInIMUSpace != BoolConfig.Default) {
            args.AddRange(new[] { "--report-in-imu", reportPosesInIMUSpace == BoolConfig.Yes ? "1" : "0" });
        }
        if (poser != Poser.Default) {
            args.AddRange(new[] { "--defaultposer", Enum.GetName(typeof(Poser), poser) });
        }
        if (seedPoser != Poser.Default) {
            args.AddRange(new[] { "--seed-poser", Enum.GetName(typeof(Poser), poser) });
        }
        if (configPoser != Poser.Default) {
            args.AddRange(new[] { "--configposer", Enum.GetName(typeof(Poser), configPoser) });
        }
        if (ootxIgnoreSyncError != BoolConfig.Default) {
            args.AddRange(new[] { "--ootx-ignore-sync-error", ootxIgnoreSyncError == BoolConfig.Yes ? "1" : "0" });
        }
        if (allTrackerCalibration != BoolConfig.Default) {
            args.AddRange(new[] { "--allowalltrackersforcal", allTrackerCalibration == BoolConfig.Yes ? "1" : "0" });
        }
        if (!string.IsNullOrEmpty(requiredCalibrationTrackers)) {
            args.AddRange(new[] { "--requiredtrackersforcal", requiredCalibrationTrackers });
        }
        if (!string.IsNullOrEmpty(configFile)) {
            args.AddRange(new[] { "-c", configFile });
        }
        return args.ToArray();
    }
}