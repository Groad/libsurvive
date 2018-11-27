using libsurvive;
using System;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class LibsurviveAPI {

    private static LibsurviveAPI _instance;
    public static LibsurviveAPI Instance {
        get => _instance ?? (_instance = new LibsurviveAPI());
    }

    public IntPtr context;

    private Thread internalPollThread;
    private bool isPolling = false;

    private LibsurviveConfig configuration;

    private bool IsReportingLight = true;
    private bool IsReportingPose = true;
    private bool IsReportingLighthousePose = true;
    private bool IsReportingAngle = true;
    private bool IsReportingButton = false;
    private bool IsReportingHtcConfig = true;
    private bool IsReportingIMU = true;
    private bool IsReportingError = true;
    private bool IsReportingInfo = true;

    public event EventHandler<LibsurvivePose> NewPose = delegate { };
    public event EventHandler<LibsurviveIMU> NewIMU = delegate { };
    public event EventHandler<LibsurviveLight> NewLight = delegate { };
    public event EventHandler<LibsurviveAngle> NewAngle = delegate { };
    public event EventHandler<LibsurviveButton> NewButton = delegate { };
    public event EventHandler<LibsurviveLighthouse> NewLighthouse = delegate { };
    public event EventHandler<LibsurviveInfo> NewInfo = delegate { };

    light_process_func      ProcessLightEvent;
    raw_pose_func           ProcessPoseEvent;
    lighthouse_pose_func    ProcessLighthousePoseEvent;
    angle_process_func      ProcessAngleEvent;
    button_process_func     ProcessButtonEvent;
    htc_config_func         ProcessHtcConfigEvent;
    imu_process_func        ProcessImuEvent;
    text_feedback_func      ProcessErrorEvent;
    text_feedback_func      ProcessInfoEvent;

    // Constructor + Destructor functions
    LibsurviveAPI() { }
    ~LibsurviveAPI() { Dispose(); }

    // Starts the library and starts the thread polling LibsurviveAPI
    public void Start(LibsurviveConfig config = null) {
        if (isPolling) {
            throw new Exception("Libsurvive is already started!");
        }
        CreateContext();
        CreateTread();
    }

    // Creates polling thread for processing libsurvive
    private void CreateTread() {
        internalPollThread = new Thread(delegate () {
            isPolling = true;
            while (isPolling) {
                if (LibsurviveFunctions.Survive_poll(context) != 0) {
                    isPolling = false;
                }
            }
        }); 
        internalPollThread.Start();
    }

    public void Close() {
        isPolling = false;
        Dispose();
    }

    private void Dispose() {
        if (context != IntPtr.Zero) {
            LibsurviveFunctions.Survive_close(context);
            context = IntPtr.Zero;
        }
    }

    // Initialize libsurvive, installs callback functions, starts processing     
    internal void CreateContext() {
        string[] args = (configuration ?? LibsurviveConfig.Default).GetParameterStrings();

        context = LibsurviveFunctions.Survive_init_internal(args.Length, args);
        if (context == IntPtr.Zero) {
            throw new Exception("There was a problem initializing the lib!");
        }
        InstallLibraryFunctions();
        try {
            if (LibsurviveFunctions.Survive_startup(context) != 0) {
                throw new Exception("Error in startup");
            }
        } catch (Exception e) {
            throw new Exception($"Exception on libsurvive startup:\n{e.Source}\n{e.Message}\n{e.StackTrace}");
        }
    }


    // Captures information events generated from the libsurvive object  
    virtual protected void InfoEvent(IntPtr ctx, string fault) {
        if (IsReportingInfo) {
            NewInfo(this, new LibsurviveInfo(fault));
        }
    }
    virtual protected void ErrorEvent(IntPtr ctx, string fault) {
        if (IsReportingError) {
            NewInfo(this, new LibsurviveInfo(fault, error:true));
        }
    }
    virtual protected int HTCConfigEvent(IntPtr so, string ct0conf, int len) {
        if (IsReportingHtcConfig) {
            NewInfo(this, new LibsurviveInfo(ct0conf, htcConfig: true, length: len));
        }
        return LibsurviveFunctions.Survive_default_htc_config_process(so, ct0conf, len);
    }

    // Captures data packets incoming from the inertial measurement unit
    virtual protected void IMUEvent(IntPtr so, int mask, IntPtr accelgyro, uint timecode, int id) {
        if (IsReportingIMU) {
            double[] gyro = new double[9];
            Marshal.Copy(accelgyro, gyro, 0, 9);
            SurviveObject _so = new SurviveObject(so);
            //Console.WriteLine($"IMU id:{id:000} time:{timecode:0000000000} mask:{mask} accel:[{gyro[0]:+0.0000;-0.0000}, {gyro[1]:+0.0000;-0.0000}, {gyro[2]:+0.0000;-0.0000}] gyro:[{gyro[3]:+0.0000;-0.0000}, {gyro[4]:+0.0000;-0.0000}, {gyro[5]:+0.0000;-0.0000}] mag:[{gyro[6]:+0.0000;-0.0000}, {gyro[7]:+0.0000;-0.0000}, {gyro[8]:+0.0000;-0.0000}]");
            NewIMU(this, new LibsurviveIMU(gyro, timecode, id, mask, _so.Name));
        }
        LibsurviveFunctions.Survive_default_imu_process(so, mask, accelgyro, timecode, id);
    }

    // Captures button events incoming from controller objects 
    virtual protected void ButtonEvent(IntPtr so, byte eventType, byte buttonId, byte axis1Id, ushort axis1Val, byte axis2Id, ushort axis2Val) {
        if (IsReportingButton) {
            var _so = new SurviveObject(so);
            NewButton(this, new LibsurviveButton(eventType, buttonId, axis1Id, axis1Val, axis2Id, axis2Val, _so.Name));
        }
        LibsurviveFunctions.Survive_default_button_process(so, eventType, buttonId, axis1Id, axis1Val, axis2Id, axis2Val);
    }

    // Captures the angle information calculated from the sensor 'sensorId' inside a tracked object 
    virtual protected void AngleEvent(IntPtr so, int sensor_id, int acode, uint timecode, double length, double angle, uint lh) {
        if (IsReportingAngle) {
            var _so = new SurviveObject(so);
            //Console.WriteLine($"ANGLE id:{sensor_id:+00;-00} sweep:{acode} time:{timecode:000000000000} len:{length:0.00000} lh:{lh} angle:{angle:N4}");
            NewAngle(this, new LibsurviveAngle(sensor_id, acode, timecode, length, angle, lh, _so.Name));
        }
        LibsurviveFunctions.Survive_default_angle_process(so, sensor_id, acode, timecode, length, angle, lh);
    }

    // Captures the calculated position of a given lighthouse after calibration 
    protected void LightHouseEvent(IntPtr ctx, byte lighthouse, SurvivePose lighthouse_pose, SurvivePose object_pose) {
        if (IsReportingLighthousePose) {
            NewLighthouse(this, new LibsurviveLighthouse(lighthouse_pose, object_pose, lighthouse));
        }
        LibsurviveFunctions.Survive_default_lighthouse_pose_process(ctx, lighthouse, lighthouse_pose, object_pose);
    }

    // Captures the light information recorded by sensor 'sensor_id' inside a tracked object 
    virtual protected void LightEvent(IntPtr so, int sensor_id, int acode, int timeinsweep, UInt32 timecode, UInt32 length, UInt32 lighthouse) {
        if (IsReportingLight) {
            var _so = new SurviveObject(so);
            //Console.WriteLine((sensor_id >= 0) ? "" : $"LIGHT id:{sensor_id:+00;-00} sweep:{acode} time:{timecode:000000000000} len:{length:0000000} lh:{lighthouse} timeinsweep:{timeinsweep:0000000}");
            NewLight(this, new LibsurviveLight(sensor_id, acode, timeinsweep, timecode, length, lighthouse, _so.Name));
        }
        LibsurviveFunctions.Survive_default_light_process(so, sensor_id, acode, timeinsweep, timecode, length, lighthouse);
    }

    // Captures the calculated poses generated by the default poser using the light, angle, and IMU data above
    virtual protected void PoseEvent(IntPtr so, byte timecode, SurvivePose pose) {
        if (IsReportingPose) {
            SurviveObject _so = new SurviveObject(so);
            NewPose(this, new LibsurvivePose(pose, _so.Name));
        }
        LibsurviveFunctions.Survive_default_raw_pose_process(so, timecode, pose);
    }

    // Gets a reference to the Survive object called 'name' 
    public SurviveObject GetSurviveObjectByName(string name) {
        if (name == "") {
            throw new Exception("Empty string is not accepted");
        }
        if (context == IntPtr.Zero) {
            throw new Exception("The context hasn't been initialsied yet");
        }
        return new SurviveObject(LibsurviveFunctions.Survive_get_so_by_name(context, name));
    }

    public CalibrationStatus GetCalibrationStatus() {
        if (context == IntPtr.Zero) {
            throw new Exception("The context hasn't been initialized yet");
        }
        return (CalibrationStatus) LibsurviveFunctions.Survive_get_cal_status(context);
    }

    // Sets the library callback functions which will receive libsurvive events for processing
    private void InstallLibraryFunctions() {
        ProcessLightEvent = LightEvent;
        ProcessPoseEvent = PoseEvent;
        ProcessLighthousePoseEvent = LightHouseEvent;
        ProcessAngleEvent = AngleEvent;
        ProcessButtonEvent = ButtonEvent;
        ProcessHtcConfigEvent = HTCConfigEvent;
        ProcessImuEvent = IMUEvent;
        ProcessErrorEvent = ErrorEvent;
        ProcessInfoEvent = InfoEvent;
        LibsurviveFunctions.Survive_install_raw_pose_fn(context, ProcessPoseEvent);
        LibsurviveFunctions.Survive_install_light_fn(context, ProcessLightEvent);
        LibsurviveFunctions.Survive_install_lighthouse_pose_fn(context, ProcessLighthousePoseEvent);
        LibsurviveFunctions.Survive_install_angle_fn(context, ProcessAngleEvent);
        LibsurviveFunctions.Survive_install_button_fn(context, ProcessButtonEvent);
        LibsurviveFunctions.Survive_install_htc_config_fn(context, ProcessHtcConfigEvent);
        LibsurviveFunctions.Survive_install_imu_fn(context, ProcessImuEvent);
        LibsurviveFunctions.Survive_install_error_fn(context, ProcessErrorEvent);
        LibsurviveFunctions.Survive_install_info_fn(context, ProcessInfoEvent);
    }
}

public class ImuSample {
    public double[] accelGyroMag;
    public double[] accelerometer   { get => new[] { accelGyroMag[0], accelGyroMag[1], accelGyroMag[2] }; }
    public double[] gyroscope       { get => new[] { accelGyroMag[3], accelGyroMag[4], accelGyroMag[5] }; }
    public double[] magnetometer    { get => new[] { accelGyroMag[6], accelGyroMag[7], accelGyroMag[8] }; }
    public uint time;
    public int mask;         // 0 = accel present, 1 = gyro present, 2 = mag present.
    public ImuSample() { }
}

public class LighthouseSweep {

    public Dictionary<int, DiodeMeasurement> activeSensors = new Dictionary<int, DiodeMeasurement>();
    public int sweepCode;
    public uint lighthouseId;

    public LighthouseSweep(LighthouseSweep lhs) {
        activeSensors = new Dictionary<int, DiodeMeasurement>(lhs.activeSensors);
        sweepCode = lhs.sweepCode;
        lighthouseId = lhs.lighthouseId;
    }
    public LighthouseSweep() { }
}

public class DiodeMeasurement {
    public int sensorId;
    public int timeInSweepTicks;
    public uint time;
    public uint pulseLengthTicks;
    public double pulseLengthSeconds;
    public double angle;

    public DiodeMeasurement(LightHit l) {
        sensorId = l.sensorId;
        timeInSweepTicks = l.timeInSweep;
        pulseLengthTicks = l.length;
        time = l.timeCode;
    }
    public void RecordAngle(LightAngle a) {
        angle = a.angle;
        pulseLengthSeconds = a.length;
    }
}

public class LightHit {
    public int sensorId;        // ID of light sensor on object
    public int sweepCode;       // OOTX Code associated with this sweep. vertical(1) or horizontal(0) sweep
    public int timeInSweep;
    public uint timeCode;       // In object-local ticks
    public uint length;         // seconds
    public uint lighthouse;     // ID of the lighthouse sweeping light
}

public class LightAngle {
    public int sensorId;
    public int sweepCode;       // OOTX Code associated with this sweep. vertical(1) or horizontal(0) sweep
    public uint timeCode;
    public double length;
    public double angle;
    public uint lighthouse;
}

public class ButtonEvent {
    public int eventType;
    public int buttonId;
    public int axis1Id, axis2Id;
    public ushort axis1Val, axis2Val;
}

public class LibsurvivePose : EventArgs {

    public SurvivePose pose;
    public string name;

    public LibsurvivePose(SurvivePose pose, string name) {
        this.pose = pose;
        this.name = name;
    }
    public override string ToString() {
        var p = pose.Pos;
        var r = pose.Rot;
        return $"P {name} {p[0]} {p[1]} {p[2]} {r[0]} {r[1]} {r[2]} {r[3]}";
    }
}

public class LibsurviveIMU : EventArgs {

    public ImuSample imu;
    public string name;

    public LibsurviveIMU(double[] gyro, uint time, int id, int mask, string name) {
        this.name = name;
        imu = new ImuSample() {
            accelGyroMag = gyro,
            time = time,
            mask = mask,
        };
    }
    public override string ToString() {
        return $"I {name} {imu.time} {imu.mask} {string.Join(" ", imu.accelGyroMag.Select(p => $"{p}"))}";
    }
}

public class LibsurviveLight : EventArgs {

    public LightHit light;
    public string name;

    public LibsurviveLight(int id, int code, int sweepTime, uint timeCode, uint length, uint lh, string name) {
        this.name = name;
        light = new LightHit() {
            sensorId = id,
            sweepCode = code,
            timeInSweep = sweepTime,
            timeCode = timeCode,
            length = length,
            lighthouse = lh,
        };
    }
    public override string ToString() {
        var l = light;
        return $"L {name} {l.sensorId} {l.sweepCode} {l.timeCode} {l.length} {l.lighthouse} {l.timeInSweep}";
    }
}

public class LibsurviveAngle : EventArgs {

    public LightAngle angle;
    public string name;
    
    public LibsurviveAngle(int id, int code, uint timeCode, double length, double angle, uint lh, string name) {
        this.name = name;
        this.angle = new LightAngle() {
            sensorId = id,
            sweepCode = code,
            timeCode = timeCode,
            length = length,
            angle = angle,
            lighthouse = lh,
        };
    }
    public override string ToString() {
        var a = angle;
        return $"A {name} {a.sensorId} {a.sweepCode} {a.timeCode} {a.length} {a.lighthouse} {a.angle}";
    }
}

public class LibsurviveLighthouse : EventArgs {

    public int lighthouse;
    public SurvivePose lighthousePose;  // Pose of lighthouse
    public SurvivePose objectPose;      // Pose of object used to calibrate the lighthouse

    public LibsurviveLighthouse(SurvivePose lhPose, SurvivePose objPose, int lh) {
        lighthouse = lh;
        lighthousePose = lhPose;
        objectPose = objPose;
    }
}

public class LibsurviveButton : EventArgs {

    public ButtonEvent button;
    public string codeName;

    public LibsurviveButton(int eventType, int button, int xId, ushort xVal, int yId, ushort yVal, string name) {
        this.button = new ButtonEvent() {
            eventType = eventType,
            buttonId = button,
            axis1Id = xId,
            axis1Val = xVal,
            axis2Id = yId,
            axis2Val = yVal,
        };
    }
}

public class LibsurviveInfo : EventArgs {
    public string info;
    public bool error;
    public bool htcConfig;
    public int length;
    public LibsurviveInfo(string info, bool error = false, bool htcConfig = false, int length = -1) {
        this.info = info;
        this.error = error;
        this.htcConfig = htcConfig;
        this.length = length;
    }
    public override string ToString() {
            string head = error ? "ERROR" : (htcConfig ? "HTC" : "INFO");
            return $"{head}: {info}";
    }
}


public enum CalibrationStatus {
    NotCalibrating          = 0,
    CollectingOOTX          = 1,
    FindingWatchman         = 2,
    CollectingSweepData     = 3,
    PoseCalculation         = 4,        // Not implemented
    LighthouseFindComplete  = 5,
}


public class SurviveObject {

    public string Name {
        get => ParseCodename();
    }

    public string DriverName {
        get => ParseDrivername();
    }

    public SurvivePose Pose {
        get => (SurvivePose) Marshal.PtrToStructure(LibsurviveFunctions.Survive_object_pose(ptr), typeof(SurvivePose));
    }

    public int Charge {
        get => LibsurviveFunctions.Survive_object_charge(ptr);
    }

    public bool Charging {
        get => LibsurviveFunctions.Survive_object_charging(ptr);
    }

    public double[] SensorLocations {
        get => LibsurviveFunctions.Survive_object_sensor_locations(ptr);
    } 

    public double[] SensorNormals {
        get => LibsurviveFunctions.Survive_object_sensor_normals(ptr);
    }

    private IntPtr ptr;
    
    public SurviveObject(IntPtr obj) {
        if (obj == IntPtr.Zero) {
            throw new Exception("Can't create SurviveObject with 0 pointer");
        }
        ptr = obj;
    }

    private string ParseCodename() {
        byte[] buffer = new byte[4];                                // 3 character codename with string terminator
        LibsurviveFunctions.Survive_object_codename(ptr, buffer);            // HMD, WM0(watchman), WW0(wired watchman)
        return System.Text.Encoding.ASCII.GetString(buffer, 0, 3);  // Just take the characters 
    }

    private string ParseDrivername() {
        byte[] buffer = new byte[8];
        LibsurviveFunctions.Survive_object_drivername(ptr, buffer);
        return System.Text.Encoding.ASCII.GetString(buffer, 0, 8);
    }
}