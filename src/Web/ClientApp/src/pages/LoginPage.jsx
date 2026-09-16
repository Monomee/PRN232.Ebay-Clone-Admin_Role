import { useState, useEffect, useRef } from "react";

const API_BASE = "/api/Auth";

// ─── Utility ────────────────────────────────────────────────────────────────
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// ─── Icons ──────────────────────────────────────────────────────────────────
const EbayLogo = () => (
  <svg viewBox="0 0 100 40" fill="none" xmlns="http://www.w3.org/2000/svg" style={{ width: 90, height: 36 }}>
    <text x="0" y="32" fontFamily="'Georgia', serif" fontSize="36" fontWeight="900" letterSpacing="-2">
      <tspan fill="#E53238">e</tspan>
      <tspan fill="#0064D2">b</tspan>
      <tspan fill="#F5AF02">a</tspan>
      <tspan fill="#86B817">y</tspan>
    </text>
  </svg>
);

const ShieldIcon = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
  </svg>
);

const LockIcon = () => (
  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
    <path d="M7 11V7a5 5 0 0 1 10 0v4" />
  </svg>
);

const MailIcon = () => (
  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z" />
    <polyline points="22,6 12,13 2,6" />
  </svg>
);

const EyeIcon = ({ off }) =>
  off ? (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94" />
      <path d="M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19" />
      <line x1="1" y1="1" x2="23" y2="23" />
    </svg>
  ) : (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
      <circle cx="12" cy="12" r="3" />
    </svg>
  );

const ArrowIcon = () => (
  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <line x1="5" y1="12" x2="19" y2="12" />
    <polyline points="12 5 19 12 12 19" />
  </svg>
);

const CheckIcon = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="20 6 9 17 4 12" />
  </svg>
);

// ─── 2FA Code Input ──────────────────────────────────────────────────────────
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
    const joined = next.join("").replace(/\s/g, "");
    onChange(joined);
    if (val && i < 5) refs.current[i + 1]?.focus();
  };

  const handlePaste = (e) => {
    const pasted = e.clipboardData.getData("text").replace(/\D/g, "").slice(0, 6);
    onChange(pasted);
    const focusIdx = Math.min(pasted.length, 5);
    refs.current[focusIdx]?.focus();
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
            width: 48,
            height: 56,
            textAlign: "center",
            fontSize: 22,
            fontWeight: 700,
            fontFamily: "'DM Mono', monospace",
            border: d ? "2px solid #0064D2" : "2px solid #dde3ef",
            borderRadius: 10,
            background: d ? "#f0f6ff" : "#fafbfd",
            color: "#0a1628",
            outline: "none",
            transition: "all 0.15s",
            cursor: disabled ? "not-allowed" : "text",
            letterSpacing: 0,
          }}
        />
      ))}
    </div>
  );
};

// ─── Spinner ─────────────────────────────────────────────────────────────────
const Spinner = () => (
  <div style={{
    width: 18, height: 18,
    border: "2.5px solid rgba(255,255,255,0.3)",
    borderTop: "2.5px solid #fff",
    borderRadius: "50%",
    animation: "spin 0.7s linear infinite",
    display: "inline-block",
  }} />
);

// ─── Main Component ──────────────────────────────────────────────────────────
export default function LoginPage() {
  const [step, setStep] = useState("login"); // "login" | "2fa" | "success"
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPw, setShowPw] = useState(false);
  const [userId, setUserId] = useState(null);
  const [code, setCode] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [shake, setShake] = useState(false);
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
    const link = document.createElement("link");
    link.rel = "stylesheet";
    link.href = "https://fonts.googleapis.com/css2?family=Playfair+Display:wght@700;800&family=DM+Sans:wght@400;500;600&family=DM+Mono:wght@400;500;700&display=swap";
    document.head.appendChild(link);
  }, []);

  const triggerShake = () => {
    setShake(true);
    setTimeout(() => setShake(false), 500);
  };

  // ── Login ──────────────────────────────────────────────────────────────────
  const handleLogin = async (e) => {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      const res = await fetch(`${API_BASE}/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",                          // ✅ gửi/nhận cookie
        body: JSON.stringify({ email, password }),
      });

      const data = await res.json();

      if (!data.success) {
        setError(data.errorMessage || "Invalid email or password");
        triggerShake();
        return;
      }

      if (data.requireTwoFactor) {
        setUserId(data.userId);
        setStep("2fa");
      } else {
        // ✅ Không lưu token — server đã set HttpOnly cookie
        setStep("success");
        await sleep(1200);
        if (data.role === "Seller") {
          window.location.href = "/seller-products";
        } else {
          window.location.href = "/";
        }
      }
    } catch (err) {
      setError("Không thể kết nối đến server. Vui lòng thử lại.");
      triggerShake();
    } finally {
      setLoading(false);
    }
  };

  // ── 2FA ───────────────────────────────────────────────────────────────────
  const handleVerify2FA = async (e) => {
    e?.preventDefault();
    if (code.length < 6) return;
    setError("");
    setLoading(true);
    try {
      const res = await fetch(`${API_BASE}/verify-2fa`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",                          // ✅ gửi/nhận cookie
        body: JSON.stringify({ userId, code }),
      });

      const data = await res.json();

      if (!data.success) {
        setError(data.errorMessage || "Incorrect 2FA code");
        setCode("");
        triggerShake();
        return;
      }

      // ✅ Không lưu token — server đã set HttpOnly cookie
      setStep("success");
      await sleep(1200);
      if (data.role === "Seller") {
        window.location.href = "/seller-products";
      } else {
        window.location.href = "/";
      }
    } catch (err) {
      setError("Không thể kết nối đến server. Vui lòng thử lại.");
      setCode("");
      triggerShake();
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (code.length === 6 && step === "2fa") handleVerify2FA();
  }, [code]);

  // ─── Styles ────────────────────────────────────────────────────────────────
  const styles = `
    @keyframes spin { to { transform: rotate(360deg); } }
    @keyframes fadeUp {
      from { opacity: 0; transform: translateY(24px); }
      to   { opacity: 1; transform: translateY(0); }
    }
    @keyframes shake {
      0%,100% { transform: translateX(0); }
      20%     { transform: translateX(-8px); }
      40%     { transform: translateX(8px); }
      60%     { transform: translateX(-6px); }
      80%     { transform: translateX(6px); }
    }
    @keyframes pulse {
      0%,100% { opacity: 1; }
      50%      { opacity: 0.5; }
    }
    @keyframes scaleIn {
      from { transform: scale(0.8); opacity: 0; }
      to   { transform: scale(1); opacity: 1; }
    }
    @keyframes float {
      0%,100% { transform: translateY(0px); }
      50%      { transform: translateY(-8px); }
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { background: #f0f4fb; }
    input:focus { outline: none; border-color: #0064D2 !important; box-shadow: 0 0 0 3px rgba(0,100,210,0.12) !important; }
    input::placeholder { color: #b0bad0; }
    button:hover:not(:disabled) { transform: translateY(-1px); }
    button:active:not(:disabled) { transform: translateY(0); }
  `;

  const card = {
    background: "#fff",
    borderRadius: 20,
    padding: "48px 44px",
    width: "100%",
    maxWidth: 440,
    boxShadow: "0 4px 6px -1px rgba(0,0,0,0.06), 0 20px 60px -10px rgba(0,64,128,0.12)",
    animation: mounted ? "fadeUp 0.5s ease both" : "none",
    ...(shake && { animation: "shake 0.45s ease" }),
  };

  const inputWrap = { position: "relative", marginBottom: 16 };

  const inputStyle = {
    width: "100%",
    padding: "14px 16px 14px 46px",
    fontSize: 15,
    fontFamily: "'DM Sans', sans-serif",
    border: "2px solid #dde3ef",
    borderRadius: 12,
    background: "#fafbfd",
    color: "#0a1628",
    transition: "all 0.2s",
  };

  const iconStyle = {
    position: "absolute",
    left: 14,
    top: "50%",
    transform: "translateY(-50%)",
    color: "#9aa5be",
    pointerEvents: "none",
  };

  const btnPrimary = {
    width: "100%",
    padding: "15px 24px",
    fontSize: 15,
    fontWeight: 600,
    fontFamily: "'DM Sans', sans-serif",
    background: "linear-gradient(135deg, #0064D2 0%, #0041a8 100%)",
    color: "#fff",
    border: "none",
    borderRadius: 12,
    cursor: loading ? "not-allowed" : "pointer",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    gap: 8,
    transition: "all 0.2s",
    opacity: loading ? 0.85 : 1,
    boxShadow: "0 4px 14px rgba(0,100,210,0.35)",
    letterSpacing: "0.3px",
  };

  return (
    <>
      <style>{styles}</style>
      <div style={{
        minHeight: "100vh",
        background: "linear-gradient(145deg, #eaf0fb 0%, #f5f7fc 50%, #e8eef8 100%)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: 20,
        fontFamily: "'DM Sans', sans-serif",
        position: "relative",
        overflow: "hidden",
      }}>
        {/* Decorative background orbs */}
        <div style={{ position: "fixed", top: -80, right: -80, width: 300, height: 300, borderRadius: "50%", background: "radial-gradient(circle, rgba(0,100,210,0.08) 0%, transparent 70%)", pointerEvents: "none" }} />
        <div style={{ position: "fixed", bottom: -100, left: -60, width: 350, height: 350, borderRadius: "50%", background: "radial-gradient(circle, rgba(134,184,23,0.07) 0%, transparent 70%)", pointerEvents: "none" }} />
        <div style={{ position: "fixed", top: "40%", left: -40, width: 200, height: 200, borderRadius: "50%", background: "radial-gradient(circle, rgba(229,50,56,0.05) 0%, transparent 70%)", pointerEvents: "none" }} />

        <div style={{ width: "100%", maxWidth: 440 }}>
          {/* Logo */}
          <div style={{ textAlign: "center", marginBottom: 32, animation: "fadeUp 0.4s ease both" }}>
            <div style={{ display: "inline-flex", alignItems: "center", gap: 10, marginBottom: 6 }}>
              <EbayLogo />
              <div style={{ width: 1, height: 28, background: "#dde3ef" }} />
              <span style={{
                fontSize: 13, fontWeight: 600, color: "#6b7a99",
                letterSpacing: "2px", textTransform: "uppercase",
                fontFamily: "'DM Sans', sans-serif",
              }}>Admin</span>
            </div>
          </div>

          <div style={card}>

            {/* ── SUCCESS ── */}
            {step === "success" && (
              <div style={{ textAlign: "center", padding: "20px 0", animation: "scaleIn 0.4s ease" }}>
                <div style={{
                  width: 68, height: 68, borderRadius: "50%",
                  background: "linear-gradient(135deg, #86B817, #5a9210)",
                  display: "flex", alignItems: "center", justifyContent: "center",
                  margin: "0 auto 20px",
                  boxShadow: "0 8px 24px rgba(134,184,23,0.35)",
                  color: "#fff",
                }}>
                  <CheckIcon />
                </div>
                <h2 style={{ fontSize: 22, fontWeight: 700, color: "#0a1628", marginBottom: 8 }}>Access Granted</h2>
                <p style={{ color: "#6b7a99", fontSize: 14 }}>Redirecting to dashboard…</p>
                <div style={{ marginTop: 20, height: 3, background: "#f0f4fb", borderRadius: 4, overflow: "hidden" }}>
                  <div style={{ height: "100%", background: "linear-gradient(90deg, #86B817, #0064D2)", borderRadius: 4, animation: "pulse 1s ease infinite" }} />
                </div>
              </div>
            )}

            {/* ── LOGIN ── */}
            {step === "login" && (
              <>
                <div style={{ marginBottom: 32 }}>
                  <h1 style={{
                    fontSize: 26, fontWeight: 800,
                    fontFamily: "'Playfair Display', serif",
                    color: "#0a1628", marginBottom: 6, letterSpacing: "-0.5px",
                  }}>Welcome back</h1>
                  <p style={{ fontSize: 14, color: "#6b7a99" }}>Sign in to your admin account</p>
                </div>

                <form onSubmit={handleLogin}>
                  <div style={inputWrap}>
                    <span style={iconStyle}><MailIcon /></span>
                    <input
                      type="email"
                      placeholder="admin@ebay.com"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      required
                      disabled={loading}
                      style={inputStyle}
                    />
                  </div>

                  <div style={{ ...inputWrap, marginBottom: 24 }}>
                    <span style={iconStyle}><LockIcon /></span>
                    <input
                      type={showPw ? "text" : "password"}
                      placeholder="Password"
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      required
                      disabled={loading}
                      style={{ ...inputStyle, paddingRight: 46 }}
                    />
                    <button
                      type="button"
                      onClick={() => setShowPw(!showPw)}
                      style={{
                        position: "absolute", right: 14, top: "50%", transform: "translateY(-50%)",
                        background: "none", border: "none", cursor: "pointer", color: "#9aa5be", padding: 2,
                      }}
                    >
                      <EyeIcon off={showPw} />
                    </button>
                  </div>

                  {error && (
                    <div style={{
                      background: "#fff0f1", border: "1.5px solid #ffd0d2",
                      borderRadius: 10, padding: "11px 14px", marginBottom: 16,
                      fontSize: 13, color: "#c0303a", fontWeight: 500,
                      display: "flex", alignItems: "center", gap: 8,
                    }}>
                      <span style={{ fontSize: 16 }}>⚠</span> {error}
                    </div>
                  )}

                  <button type="submit" disabled={loading} style={btnPrimary}>
                    {loading ? <Spinner /> : <><span>Sign In</span><ArrowIcon /></>}
                  </button>
                </form>

                <div style={{ marginTop: 28, paddingTop: 24, borderTop: "1.5px solid #f0f3f9" }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 8, color: "#9aa5be", fontSize: 12 }}>
                    <LockIcon />
                    <span>Secured with 256-bit SSL encryption</span>
                  </div>
                </div>
              </>
            )}

            {/* ── 2FA ── */}
            {step === "2fa" && (
              <>
                <div style={{ textAlign: "center", marginBottom: 32 }}>
                  <div style={{
                    width: 56, height: 56, borderRadius: 16,
                    background: "linear-gradient(135deg, #eef4ff, #dde8fb)",
                    display: "flex", alignItems: "center", justifyContent: "center",
                    margin: "0 auto 16px",
                    color: "#0064D2",
                    animation: "float 3s ease-in-out infinite",
                  }}>
                    <ShieldIcon />
                  </div>
                  <h1 style={{
                    fontSize: 24, fontWeight: 800,
                    fontFamily: "'Playfair Display', serif",
                    color: "#0a1628", marginBottom: 6,
                  }}>Two-Factor Auth</h1>
                  <p style={{ fontSize: 14, color: "#6b7a99", lineHeight: 1.6 }}>
                    Open your authenticator app and<br />enter the 6-digit code
                  </p>
                </div>

                <form onSubmit={handleVerify2FA}>
                  <div style={{ marginBottom: 24 }}>
                    <CodeInput value={code} onChange={setCode} disabled={loading} />
                  </div>

                  {error && (
                    <div style={{
                      background: "#fff0f1", border: "1.5px solid #ffd0d2",
                      borderRadius: 10, padding: "11px 14px", marginBottom: 16,
                      fontSize: 13, color: "#c0303a", fontWeight: 500,
                      display: "flex", alignItems: "center", gap: 8,
                    }}>
                      <span style={{ fontSize: 16 }}>⚠</span> {error}
                    </div>
                  )}

                  <button type="submit" disabled={loading || code.length < 6} style={{
                    ...btnPrimary,
                    opacity: (loading || code.length < 6) ? 0.5 : 1,
                    cursor: (loading || code.length < 6) ? "not-allowed" : "pointer",
                  }}>
                    {loading ? <Spinner /> : <><span>Verify Code</span><ArrowIcon /></>}
                  </button>

                  <button
                    type="button"
                    onClick={() => { setStep("login"); setError(""); setCode(""); }}
                    style={{
                      width: "100%", marginTop: 12,
                      padding: "13px 24px", fontSize: 14, fontWeight: 500,
                      fontFamily: "'DM Sans', sans-serif",
                      background: "transparent", color: "#6b7a99",
                      border: "2px solid #eaeff8", borderRadius: 12, cursor: "pointer",
                      transition: "all 0.2s",
                    }}
                    onMouseEnter={(e) => { e.target.style.background = "#f5f7fc"; e.target.style.color = "#0a1628"; }}
                    onMouseLeave={(e) => { e.target.style.background = "transparent"; e.target.style.color = "#6b7a99"; }}
                  >
                    ← Back to Sign In
                  </button>
                </form>

                <div style={{ marginTop: 24, textAlign: "center" }}>
                  <p style={{ fontSize: 12, color: "#b0bad0" }}>Code refreshes every 30 seconds</p>
                </div>
              </>
            )}
          </div>

          {/* Footer */}
          <p style={{ textAlign: "center", marginTop: 20, fontSize: 12, color: "#b0bad0", animation: "fadeUp 0.6s ease 0.2s both" }}>
            © {new Date().getFullYear()} eBay Inc. · Admin Portal · All rights reserved
          </p>
        </div>
      </div>
    </>
  );
}