using System;
using System.Collections.Generic;
using System.IO.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ZLogger;

namespace DGAIZone.Game.Hardware
{
    /// <summary>
    /// 윈도우 레지스트리를 조회하여 USB VID/PID에 대응하는 활성 COM 포트를 동적으로 탐색하는 유틸리티.
    /// </summary>
    public static class SerialPortFinder
    {
        /// <summary>
        /// 지정된 VID와 PID를 포함하는 USB 시리얼 장치의 현재 활성 COM 포트 이름을 반환함.
        /// </summary>
        public static string FindPortByVidPid(string targetVid, string targetPid, ILogger logger = null)
        {
            List<(string port, string vidPid, string desc)> activePorts = GetAllActiveUsbSerialPorts();

            if (logger != null)
            {
                logger.ZLogInformation($"[SerialPortFinder] Active USB Serial Ports diagnostic count: {activePorts.Count}");
                foreach (var info in activePorts)
                {
                    logger.ZLogInformation($"[SerialPortFinder] Detected Port: {info.port} | ID: {info.vidPid} | Desc: {info.desc}");
                }
            }

            if (string.IsNullOrWhiteSpace(targetVid) && string.IsNullOrWhiteSpace(targetPid))
            {
                if (logger != null) logger.ZLogWarning($"[SerialPortFinder] Target VID and PID are empty. Skipping discovery.");
                return null;
            }

            string cleanVid = targetVid?.Trim().ToUpper() ?? "";
            string cleanPid = targetPid?.Trim().ToUpper() ?? "";

            foreach (var info in activePorts)
            {
                string upperId = info.vidPid.ToUpper();
                bool vidMatch = string.IsNullOrEmpty(cleanVid) || upperId.Contains($"VID_{cleanVid}") || upperId.Contains(cleanVid);
                bool pidMatch = string.IsNullOrEmpty(cleanPid) || upperId.Contains($"PID_{cleanPid}") || upperId.Contains(cleanPid);

                if (vidMatch && pidMatch)
                {
                    if (logger != null) logger.ZLogInformation($"[SerialPortFinder] Successfully matched RFID Reader at {info.port} (VID: {cleanVid}, PID: {cleanPid})");
                    return info.port;
                }
            }

            if (logger != null) logger.ZLogWarning($"[SerialPortFinder] Could not find active port matching VID: {cleanVid}, PID: {cleanPid}");
            return null;
        }

        /// <summary>
        /// 지정된 장치 인스턴스 경로(Device Instance Path)를 기준으로 활성 COM 포트를 탐색함.
        /// </summary>
        public static string FindPortByInstancePath(string targetInstancePath, ILogger logger = null)
        {
            if (string.IsNullOrWhiteSpace(targetInstancePath))
            {
                if (logger != null) logger.ZLogWarning($"[SerialPortFinder] Target Instance Path is empty. Skipping discovery.");
                return null;
            }

            List<(string port, string vidPid, string desc)> activePorts = GetAllActiveUsbSerialPorts();
            string cleanPath = targetInstancePath.Trim().Replace("/", "\\").ToUpper();

            foreach (var info in activePorts)
            {
                string upperId = info.vidPid.ToUpper();
                if (upperId == cleanPath || upperId.Contains(cleanPath))
                {
                    if (logger != null) logger.ZLogInformation($"[SerialPortFinder] Successfully matched RFID Reader at {info.port} (InstancePath: {targetInstancePath})");
                    return info.port;
                }
            }

            if (logger != null) logger.ZLogWarning($"[SerialPortFinder] Could not find active port matching Instance Path: {targetInstancePath}");
            return null;
        }

        /// <summary>
        /// 레지스트리와 현재 활성 포트 목록을 대조하여 연결된 모든 USB 시리얼 장치 정보를 수집함.
        /// </summary>
        private static List<(string port, string vidPid, string desc)> GetAllActiveUsbSerialPorts()
        {
            var results = new List<(string port, string vidPid, string desc)>();
            var activePortNames = new HashSet<string>(SerialPort.GetPortNames(), StringComparer.OrdinalIgnoreCase);

            if (activePortNames.Count == 0)
            {
                return results;
            }

            try
            {
                using (RegistryKey enumKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum"))
                {
                    if (enumKey != null)
                    {
                        foreach (string busName in enumKey.GetSubKeyNames())
                        {
                            using (RegistryKey busKey = enumKey.OpenSubKey(busName))
                            {
                                if (busKey == null) continue;

                                foreach (string deviceId in busKey.GetSubKeyNames())
                                {
                                    using (RegistryKey deviceKey = busKey.OpenSubKey(deviceId))
                                    {
                                        if (deviceKey == null) continue;

                                        foreach (string instanceId in deviceKey.GetSubKeyNames())
                                        {
                                            using (RegistryKey instanceKey = deviceKey.OpenSubKey(instanceId))
                                            {
                                                if (instanceKey == null) continue;

                                                using (RegistryKey paramKey = instanceKey.OpenSubKey("Device Parameters"))
                                                {
                                                    if (paramKey == null) continue;

                                                    string portName = paramKey.GetValue("PortName") as string;
                                                    if (!string.IsNullOrEmpty(portName) && activePortNames.Contains(portName))
                                                    {
                                                        string desc = instanceKey.GetValue("FriendlyName") as string ?? instanceKey.GetValue("DeviceDesc") as string ?? "Unknown Device";
                                                        string fullId = $"{busName}\\{deviceId}\\{instanceId}";

                                                        if (!results.Exists(r => string.Equals(r.port, portName, StringComparison.OrdinalIgnoreCase)))
                                                        {
                                                            results.Add((portName, fullId, desc));
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 레지스트리 조회 중 예외 발생 시 무시하고 진행
            }

            foreach (string port in activePortNames)
            {
                if (!results.Exists(r => string.Equals(r.port, port, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add((port, "Standard/Virtual Port", "Serial Port"));
                }
            }

            return results;
        }
    }
}
