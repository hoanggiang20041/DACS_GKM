using System;

namespace Chamsoc.Models
{
    public class WebRTCData
    {
        public string Type { get; set; }
        public string Sdp { get; set; }
    }

    public class IceCandidateData
    {
        public string Candidate { get; set; }
        public string SdpMid { get; set; }
        public int? SdpMLineIndex { get; set; }
    }
} 