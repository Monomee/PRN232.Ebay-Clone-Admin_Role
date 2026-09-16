import { useState, useEffect } from "react";
import { useRef } from "react";
import { useNavigate } from "react-router-dom";


const API_BASE = "/api/Auth";

// ─── Icons ──────────────────────────────────────────────────────────────────
const EbayLogo = () => (
  <svg viewBox="0 0 100 40" fill="none" xmlns="http://www.w3.org/2000/svg" style={{ width: 80, height: 32 }}>
    <text x="0" y="32" fontFamily="'Georgia', serif" fontSize="36" fontWeight="900" letterSpacing="-2">
      <tspan fill="#E53238">e</tspan>
      <tspan fill="#0064D2">b</tspan>
      <tspan fill="#F5AF02">a</tspan>
      <tspan fill="#86B817">y</tspan>
    </text>
  </svg>
);

const ShieldIcon = ({ size = 24 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
  </svg>
);

const CheckCircleIcon = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
    <polyline points="22 4 12 14.01 9 11.01" />
  </svg>
);

const ScanIcon = () => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
  </svg>
);

const KeyIcon = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M21 2l-2 2m-7.61 7.61a5.5 5.5 0 1 1-7.778 7.778 5.5 5.5 0 0 1 7.777-7.777zm0 0L15.5 7.5m0 0l3 3L22 7l-3-3m-3.5 3.5L19 4" />
  </svg>
);

const ArrowRightIcon = () => (
  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <line x1="5" y1="12" x2="19" y2="12" />
    <polyline points="12 5 19 12 12 19" />
  </svg>
);

const BackIcon = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <line x1="19" y1="12" x2="5" y2="12" />
    <polyline points="12 19 5 12 12 5" />
  </svg>
);

const PhoneIcon = () => (
  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <rect x="5" y="2" width="14" height="20" rx="2" ry="2" />
    <line x1="12" y1="18" x2="12.01" y2="18" />
  </svg>
);

// ─── CodeInput ────────────────────────────────────────────────────────────────

const CodeInput = ({ value, onChange, disabled }) => {
  const refs = useRef([]);
  const digits = value.split("").concat(Array(6).fill("")).slice(0, 6);

  const handleKey = (i, e) => {
    if (e.key === "Backspace" && !digits[i] && i > 0) refs.current[i - 1]?.focus();
  };

  const handleChange = (i, e) => {
    const val = e.target.value.replace(/\D/g, "").slice(-1);
    const next = [...digits];
    next[i] = val;
    onChange(next.join(""));
    if (val && i < 5) refs.current[i + 1]?.focus();
  };

  const handlePaste = (e) => {
    const pasted = e.clipboardData.getData("text").replace(/\D/g, "").slice(0, 6);
    onChange(pasted);
    refs.current[Math.min(pasted.length, 5)]?.focus();
    e.preventDefault();
  };

  return (
    <div style={{ display: "flex", gap: 10, justifyContent: "center" }}>
      {digits.map((d, i) => (
        <input
          key={i}
          ref={(el) => (refs.current[i] = el)}
          type="text"
          inputMode="numeric"
          maxLength={1}
          value={d}
          disabled={disabled}
          onChange={(e) => handleChange(i, e)}
          onKeyDown={(e) => handleKey(i, e)}
          onPaste={handlePaste}
          style={{
            width: 48, height: 56,
            textAlign: "center",
            fontSize: 22, fontWeight: 700,
            fontFamily: "'DM Mono', monospace",
            border: d ? "2px solid #0064D2" : "2px solid #dde3ef",
            borderRadius: 10,
            background: d ? "#f0f6ff" : "#fafbfd",
            color: "#0a1628",
            outline: "none",
            transition: "all 0.15s",
            cursor: disabled ? "not-allowed" : "text",
          }}
        />
      ))}
    </div>
  );
};

// ─── Spinner ──────────────────────────────────────────────────────────────────
const Spinner = ({ color = "#fff" }) => (
  <div style={{
    width: 18, height: 18,
    border: `2.5px solid ${color}33`,
    borderTop: `2.5px solid ${color}`,
    borderRadius: "50%",
    animation: "spin 0.7s linear infinite",
    display: "inline-block",
  }} />
);

// ─── Step Indicator ───────────────────────────────────────────────────────────
const StepIndicator = ({ current }) => {
  const steps = [
    { n: 1, label: "Setup" },
    { n: 2, label: "Scan QR" },
    { n: 3, label: "Verify" },
  ];

  return (
    <div style={{ display: "flex", alignItems: "center", justifyContent: "center", marginBottom: 36, gap: 0 }}>
      {steps.map((s, i) => (
        <div key={s.n} style={{ display: "flex", alignItems: "center" }}>
          <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 6 }}>
            <div style={{
              width: 34, height: 34, borderRadius: "50%",
              display: "flex", alignItems: "center", justifyContent: "center",
              fontSize: 13, fontWeight: 700,
              background: current >= s.n
                ? "linear-gradient(135deg, #0064D2, #0041a8)"
                : "#f0f3f9",
              color: current >= s.n ? "#fff" : "#9aa5be",
              boxShadow: current === s.n ? "0 4px 12px rgba(0,100,210,0.3)" : "none",
              transition: "all 0.3s",
            }}>
              {current > s.n ? "✓" : s.n}
            </div>
            <span style={{
              fontSize: 11, fontWeight: 600,
              color: current >= s.n ? "#0064D2" : "#9aa5be",
              letterSpacing: "0.5px", textTransform: "uppercase",
            }}>{s.label}</span>
          </div>
          {i < steps.length - 1 && (
            <div style={{
              width: 60, height: 2, marginBottom: 20,
              background: current > s.n
                ? "linear-gradient(90deg, #0064D2, #0041a8)"
                : "#e8edf5",
              transition: "all 0.3s",
            }} />
          )}
        </div>
      ))}
    </div>
  );
};

// ─── Main Component ───────────────────────────────────────────────────────────
export default function Enable2FAPage() {
  const [step, setStep] = useState(1);         // 1: intro | 2: qr | 3: verify | 4: done
  const [qrCode, setQrCode] = useState("");
  const [code, setCode] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [mounted, setMounted] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    setMounted(true);
    const link = document.createElement("link");
    link.rel = "stylesheet";
    link.href = "https://fonts.googleapis.com/css2?family=Playfair+Display:wght@700;800&family=DM+Sans:wght@400;500;600&family=DM+Mono:wght@400;500;700&display=swap";
    document.head.appendChild(link);
  }, []);

  // ── Step 1 → 2: Gọi API enable-2fa, nhận QR ──────────────────────────────
  const handleEnable = async () => {
    setLoading(true);
    setError("");
    try {
      const res = await fetch(`${API_BASE}/enable-2fa`, {
        method: "POST",
        credentials: "include", // ✅ gửi cookie
      });
      const data = await res.json();

      if (!res.ok || !data.success)
        throw new Error(data.message || "Failed to enable 2FA");

      setQrCode(data.qrCode); // base64 PNG từ server
      setStep(2);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  // ── Step 3: Verify mã sau khi scan ───────────────────────────────────────
  const handleVerify = async (e) => {
    e?.preventDefault();
    if (code.length < 6) return;
    setLoading(true);
    setError("");
    try {
      const res = await fetch(`${API_BASE}/verify-2fa-setup`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ code }),
      });
      const data = await res.json();

      if (!res.ok || !data.success)
        throw new Error(data.errorMessage || "Invalid code, please try again");

      setStep(4);
    } catch (err) {
      setError(err.message);
      setCode("");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (code.length === 6 && step === 3) handleVerify();
  }, [code]);

  // ─── Styles ────────────────────────────────────────────────────────────────
  const css = `
    @keyframes spin    { to { transform: rotate(360deg); } }
    @keyframes fadeUp  { from { opacity:0; transform:translateY(20px); } to { opacity:1; transform:translateY(0); } }
    @keyframes scaleIn { from { transform:scale(0.85); opacity:0; } to { transform:scale(1); opacity:1; } }
    @keyframes pulse   { 0%,100% { opacity:1; } 50% { opacity:0.4; } }
    @keyframes shimmer {
      0%   { background-position: -400px 0; }
      100% { background-position: 400px 0; }
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { background: #f0f4fb; }
    input:focus { outline: none; border-color: #0064D2 !important; box-shadow: 0 0 0 3px rgba(0,100,210,0.12) !important; }
    button:hover:not(:disabled) { transform: translateY(-1px); }
    button:active:not(:disabled) { transform: translateY(0px); }
  `;

  const card = {
    background: "#fff",
    borderRadius: 20,
    padding: "44px 44px",
    width: "100%",
    maxWidth: 460,
    boxShadow: "0 4px 6px -1px rgba(0,0,0,0.06), 0 20px 60px -10px rgba(0,64,128,0.12)",
    animation: mounted ? "fadeUp 0.5s ease both" : "none",
  };

  const btnPrimary = (disabled) => ({
    width: "100%",
    padding: "15px 24px",
    fontSize: 15, fontWeight: 600,
    fontFamily: "'DM Sans', sans-serif",
    background: disabled
      ? "#c8d8f0"
      : "linear-gradient(135deg, #0064D2 0%, #0041a8 100%)",
    color: "#fff",
    border: "none", borderRadius: 12,
    cursor: disabled ? "not-allowed" : "pointer",
    display: "flex", alignItems: "center", justifyContent: "center", gap: 8,
    transition: "all 0.2s",
    boxShadow: disabled ? "none" : "0 4px 14px rgba(0,100,210,0.3)",
    letterSpacing: "0.3px",
  });

  const errorBox = error && (
    <div style={{
      background: "#fff0f1", border: "1.5px solid #ffd0d2",
      borderRadius: 10, padding: "11px 14px", marginBottom: 16,
      fontSize: 13, color: "#c0303a", fontWeight: 500,
      display: "flex", alignItems: "center", gap: 8,
    }}>
      <span style={{ fontSize: 16 }}>⚠</span> {error}
    </div>
  );

  return (
    <>
      <style>{css}</style>
      <div style={{
        minHeight: "100vh",
        background: "linear-gradient(145deg, #eaf0fb 0%, #f5f7fc 50%, #e8eef8 100%)",
        display: "flex", alignItems: "center", justifyContent: "center",
        padding: 20,
        fontFamily: "'DM Sans', sans-serif",
        position: "relative", overflow: "hidden",
      }}>
        {/* Orbs */}
        <div style={{ position: "fixed", top: -80, right: -80, width: 300, height: 300, borderRadius: "50%", background: "radial-gradient(circle, rgba(0,100,210,0.08) 0%, transparent 70%)", pointerEvents: "none" }} />
        <div style={{ position: "fixed", bottom: -100, left: -60, width: 350, height: 350, borderRadius: "50%", background: "radial-gradient(circle, rgba(134,184,23,0.07) 0%, transparent 70%)", pointerEvents: "none" }} />

        <div style={{ width: "100%", maxWidth: 460 }}>
          {/* Header */}
          <div style={{ textAlign: "center", marginBottom: 28, animation: "fadeUp 0.4s ease both" }}>
            <div style={{ display: "inline-flex", alignItems: "center", gap: 10 }}>
              <EbayLogo />
              <div style={{ width: 1, height: 26, background: "#dde3ef" }} />
              <span style={{ fontSize: 12, fontWeight: 600, color: "#6b7a99", letterSpacing: "2px", textTransform: "uppercase" }}>
                Security
              </span>
            </div>
          </div>

          <div style={card}>

            {/* ── STEP 1: Intro ── */}
            {step === 1 && (
              <div style={{ animation: "fadeUp 0.4s ease" }}>
                <div style={{ textAlign: "center", marginBottom: 32 }}>
                  <div style={{
                    width: 64, height: 64, borderRadius: 18,
                    background: "linear-gradient(135deg, #eef4ff, #dde8fb)",
                    display: "flex", alignItems: "center", justifyContent: "center",
                    margin: "0 auto 16px", color: "#0064D2",
                  }}>
                    <ShieldIcon size={28} />
                  </div>
                  <h1 style={{
                    fontSize: 26, fontWeight: 800,
                    fontFamily: "'Playfair Display', serif",
                    color: "#0a1628", marginBottom: 8, letterSpacing: "-0.5px",
                  }}>
                    Enable Two-Factor Auth
                  </h1>
                  <p style={{ fontSize: 14, color: "#6b7a99", lineHeight: 1.7 }}>
                    Add an extra layer of security to your account.<br />
                    You'll need an authenticator app on your phone.
                  </p>
                </div>

                {/* Info cards */}
                <div style={{ display: "flex", flexDirection: "column", gap: 10, marginBottom: 28 }}>
                  {[
                    { icon: <PhoneIcon />, title: "Get an authenticator app", desc: "Google Authenticator, Authy, or Microsoft Authenticator" },
                    { icon: <ScanIcon />,  title: "Scan the QR code",         desc: "Use the app to scan a QR code we'll generate for you" },
                    { icon: <KeyIcon />,   title: "Enter the 6-digit code",   desc: "Verify the setup by entering the code from your app" },
                  ].map((item, i) => (
                    <div key={i} style={{
                      display: "flex", alignItems: "flex-start", gap: 14,
                      padding: "14px 16px", borderRadius: 12,
                      background: "#f8faff", border: "1.5px solid #eaeff8",
                    }}>
                      <div style={{
                        width: 36, height: 36, borderRadius: 10, flexShrink: 0,
                        background: "linear-gradient(135deg, #eef4ff, #dde8fb)",
                        display: "flex", alignItems: "center", justifyContent: "center",
                        color: "#0064D2",
                      }}>
                        {item.icon}
                      </div>
                      <div>
                        <p style={{ fontSize: 13, fontWeight: 600, color: "#0a1628", marginBottom: 2 }}>{item.title}</p>
                        <p style={{ fontSize: 12, color: "#9aa5be", lineHeight: 1.5 }}>{item.desc}</p>
                      </div>
                    </div>
                  ))}
                </div>

                {errorBox}

                <button onClick={handleEnable} disabled={loading} style={btnPrimary(loading)}>
                  {loading ? <Spinner /> : <><span>Get Started</span><ArrowRightIcon /></>}
                </button>

                <button
                  onClick={() => navigate("/Login")}
                  style={{
                    width: "100%", marginTop: 10, padding: "13px",
                    fontSize: 14, fontWeight: 500, fontFamily: "'DM Sans', sans-serif",
                    background: "transparent", color: "#9aa5be",
                    border: "2px solid #eaeff8", borderRadius: 12, cursor: "pointer",
                    transition: "all 0.2s",
                  }}
                  onMouseEnter={(e) => { e.target.style.color = "#0a1628"; e.target.style.borderColor = "#c8d5e8"; }}
                  onMouseLeave={(e) => { e.target.style.color = "#9aa5be"; e.target.style.borderColor = "#eaeff8"; }}
                >
                  Maybe later
                </button>
              </div>
            )}

            {/* ── STEP 2: QR Code ── */}
            {step === 2 && (
              <div style={{ animation: "fadeUp 0.4s ease" }}>
                <StepIndicator current={2} />

                <div style={{ textAlign: "center", marginBottom: 28 }}>
                  <h2 style={{
                    fontSize: 22, fontWeight: 800,
                    fontFamily: "'Playfair Display', serif",
                    color: "#0a1628", marginBottom: 6,
                  }}>Scan QR Code</h2>
                  <p style={{ fontSize: 14, color: "#6b7a99" }}>
                    Open your authenticator app and scan this code
                  </p>
                </div>

                {/* QR Code display */}
                <div style={{
                  display: "flex", justifyContent: "center", marginBottom: 24,
                }}>
                  <div style={{
                    padding: 16, borderRadius: 16,
                    background: "#fff",
                    border: "2px solid #eaeff8",
                    boxShadow: "0 4px 20px rgba(0,64,128,0.08)",
                  }}>
                    {qrCode ? (
                      <img
                        src={`data:image/png;base64,${qrCode}`}
                        alt="2FA QR Code"
                        style={{ width: 180, height: 180, display: "block" }}
                      />
                    ) : (
                      <div style={{
                        width: 180, height: 180,
                        background: "linear-gradient(90deg, #f0f4fb 25%, #e4ebf5 50%, #f0f4fb 75%)",
                        backgroundSize: "400px 100%",
                        animation: "shimmer 1.2s infinite",
                        borderRadius: 8,
                      }} />
                    )}
                  </div>
                </div>

                <div style={{
                  background: "#fffbeb", border: "1.5px solid #fde68a",
                  borderRadius: 10, padding: "11px 14px", marginBottom: 24,
                  fontSize: 12, color: "#92400e",
                  display: "flex", alignItems: "flex-start", gap: 8,
                }}>
                  <span style={{ fontSize: 15, marginTop: 1 }}>💡</span>
                  <span>Keep this QR code private. Anyone with access can add your account to their authenticator app.</span>
                </div>

                <button
                  onClick={() => setStep(3)}
                  style={btnPrimary(false)}
                >
                  <span>I've scanned it</span><ArrowRightIcon />
                </button>

                <button
                  onClick={() => setStep(1)}
                  style={{
                    width: "100%", marginTop: 10, padding: "13px",
                    fontSize: 14, fontWeight: 500, fontFamily: "'DM Sans', sans-serif",
                    background: "transparent", color: "#9aa5be",
                    border: "2px solid #eaeff8", borderRadius: 12, cursor: "pointer",
                    display: "flex", alignItems: "center", justifyContent: "center", gap: 6,
                    transition: "all 0.2s",
                  }}
                  onMouseEnter={(e) => { e.currentTarget.style.color = "#0a1628"; }}
                  onMouseLeave={(e) => { e.currentTarget.style.color = "#9aa5be"; }}
                >
                  <BackIcon /> Back
                </button>
              </div>
            )}

            {/* ── STEP 3: Verify ── */}
            {step === 3 && (
              <div style={{ animation: "fadeUp 0.4s ease" }}>
                <StepIndicator current={3} />

                <div style={{ textAlign: "center", marginBottom: 28 }}>
                  <h2 style={{
                    fontSize: 22, fontWeight: 800,
                    fontFamily: "'Playfair Display', serif",
                    color: "#0a1628", marginBottom: 6,
                  }}>Enter Verification Code</h2>
                  <p style={{ fontSize: 14, color: "#6b7a99", lineHeight: 1.6 }}>
                    Enter the 6-digit code from your authenticator app<br />to confirm the setup
                  </p>
                </div>

                <form onSubmit={handleVerify}>
                  <div style={{ marginBottom: 24 }}>
                    <CodeInput value={code} onChange={setCode} disabled={loading} />
                  </div>

                  {errorBox}

                  <button
                    type="submit"
                    disabled={loading || code.length < 6}
                    style={btnPrimary(loading || code.length < 6)}
                  >
                    {loading ? <Spinner /> : <><span>Verify & Activate</span><ArrowRightIcon /></>}
                  </button>
                </form>

                <button
                  onClick={() => { setStep(2); setCode(""); setError(""); }}
                  style={{
                    width: "100%", marginTop: 10, padding: "13px",
                    fontSize: 14, fontWeight: 500, fontFamily: "'DM Sans', sans-serif",
                    background: "transparent", color: "#9aa5be",
                    border: "2px solid #eaeff8", borderRadius: 12, cursor: "pointer",
                    display: "flex", alignItems: "center", justifyContent: "center", gap: 6,
                    transition: "all 0.2s",
                  }}
                  onMouseEnter={(e) => { e.currentTarget.style.color = "#0a1628"; }}
                  onMouseLeave={(e) => { e.currentTarget.style.color = "#9aa5be"; }}
                >
                  <BackIcon /> Back to QR Code
                </button>

                <p style={{ textAlign: "center", marginTop: 16, fontSize: 12, color: "#b0bad0" }}>
                  Code refreshes every 30 seconds
                </p>
              </div>
            )}

            {/* ── STEP 4: Done ── */}
            {step === 4 && (
              <div style={{ textAlign: "center", padding: "16px 0", animation: "scaleIn 0.4s ease" }}>
                <div style={{
                  width: 72, height: 72, borderRadius: "50%",
                  background: "linear-gradient(135deg, #86B817, #5a9210)",
                  display: "flex", alignItems: "center", justifyContent: "center",
                  margin: "0 auto 20px",
                  boxShadow: "0 8px 24px rgba(134,184,23,0.35)",
                  color: "#fff",
                }}>
                  <CheckCircleIcon />
                </div>

                <h2 style={{
                  fontSize: 24, fontWeight: 800,
                  fontFamily: "'Playfair Display', serif",
                  color: "#0a1628", marginBottom: 8,
                }}>
                  2FA Activated!
                </h2>
                <p style={{ fontSize: 14, color: "#6b7a99", lineHeight: 1.7, marginBottom: 32 }}>
                  Your account is now protected with<br />two-factor authentication.
                </p>

                <div style={{
                  background: "#f0fdf4", border: "1.5px solid #bbf7d0",
                  borderRadius: 12, padding: "14px 18px", marginBottom: 28,
                  fontSize: 13, color: "#166534", textAlign: "left",
                  display: "flex", alignItems: "flex-start", gap: 10,
                }}>
                  <span style={{ fontSize: 16, marginTop: 1 }}>🔒</span>
                  <span>Next time you log in, you'll be asked for a code from your authenticator app.</span>
                </div>

                <button
                  onClick={() => window.location.href = "/login"}
                  style={btnPrimary(false)}
                >
                  <span>Go to Dashboard</span><ArrowRightIcon />
                </button>
              </div>
            )}
          </div>

          <p style={{ textAlign: "center", marginTop: 20, fontSize: 12, color: "#b0bad0" }}>
            © {new Date().getFullYear()} eBay Inc. · Admin Portal · All rights reserved
          </p>
        </div>
      </div>
    </>
  );
}