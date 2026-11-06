import { useEffect, useRef, useState } from "react";
import { apiRequest } from "../services/httpClient";

const API = "/api/Auth";
const TURNSTILE_SRC = "https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit";

// Loads Cloudflare Turnstile once and renders it into `container`; resolves to the widget id.
function renderTurnstile(container, siteKey, onToken) {
  const render = () => window.turnstile.render(container, {
    sitekey: siteKey,
    callback: onToken,
    "expired-callback": () => onToken(""),
  });
  if (window.turnstile) return Promise.resolve(render());
  return new Promise((resolve, reject) => {
    let script = document.querySelector(`script[src="${TURNSTILE_SRC}"]`);
    if (!script) {
      script = document.createElement("script");
      script.src = TURNSTILE_SRC;
      script.async = true;
      document.head.appendChild(script);
    }
    script.addEventListener("load", () => resolve(render()));
    script.addEventListener("error", reject);
  });
}

const emptyForm = {
  role: "Buyer", email: "", username: "", password: "", phone: "",
  fullName: "", cccd: "", dateOfBirth: "",
};

/// Self sign-up for buyers and sellers: form (+ captcha) -> 6-digit code from the email -> waits for the risk review.
export default function RegisterPage() {
  const [step, setStep] = useState("form"); // form | code | done
  const [form, setForm] = useState(emptyForm);
  const [code, setCode] = useState("");
  const [siteKey, setSiteKey] = useState(null);
  const [captchaToken, setCaptchaToken] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [info, setInfo] = useState("");
  const captchaRef = useRef(null);
  const widgetId = useRef(null);

  useEffect(() => {
    apiRequest(`${API}/captcha-config`)
      .then((config) => setSiteKey(config?.turnstileSiteKey || ""))
      .catch(() => setSiteKey(""));
  }, []);

  useEffect(() => {
    // No site key: the server skips the captcha outside Production (and refuses sign-ups in Production).
    if (!siteKey || step !== "form" || !captchaRef.current || widgetId.current !== null) return;
    renderTurnstile(captchaRef.current, siteKey, setCaptchaToken)
      .then((id) => { widgetId.current = id; })
      .catch(() => setError("Không tải được captcha, vui lòng tải lại trang."));
  }, [siteKey, step]);

  const set = (field) => (e) => setForm({ ...form, [field]: e.target.value });
  const needsKyc = form.role === "Seller" || form.cccd || form.fullName || form.dateOfBirth;

  const register = async (e) => {
    e.preventDefault();
    setBusy(true);
    setError("");
    try {
      const body = { ...form, captchaToken };
      Object.keys(body).forEach((k) => { if (body[k] === "") body[k] = null; });
      const result = await apiRequest(`${API}/register`, { method: "POST", body });
      setInfo(result.message);
      setStep("code");
    } catch (err) {
      setError(err.message);
      if (widgetId.current !== null) window.turnstile?.reset(widgetId.current); // a token is single-use
      setCaptchaToken("");
    } finally {
      setBusy(false);
    }
  };

  const verify = async (e) => {
    e.preventDefault();
    setBusy(true);
    setError("");
    try {
      const result = await apiRequest(`${API}/verify-email`, { method: "POST", body: { email: form.email, code } });
      if (result.success) { setInfo(result.message); setStep("done"); }
      else setError(result.errorMessage);
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  };

  const resend = async () => {
    setError("");
    try {
      const result = await apiRequest(`${API}/resend-email-code`, { method: "POST", body: { email: form.email } });
      setInfo(result.message || result.errorMessage);
    } catch (err) {
      setError(err.message);
    }
  };

  return (
    <div className="min-vh-100 d-flex align-items-center justify-content-center bg-light py-5 px-3">
      <div className="card border-0 shadow-sm rounded-4 w-100" style={{ maxWidth: 520 }}>
        <div className="card-body p-4 p-md-5">
          <h1 className="h4 fw-bold mb-1">Create an account</h1>
          <p className="text-secondary small mb-4">Buyer or seller. Seller accounts need identity details (CCCD).</p>

          {error && <div className="alert alert-danger py-2 small" role="alert">{error}</div>}
          {info && step !== "form" && <div className="alert alert-info py-2 small" role="status">{info}</div>}

          {step === "form" && (
            <form onSubmit={register} noValidate>
              <div className="btn-group w-100 mb-3" role="group" aria-label="Account type">
                {["Buyer", "Seller"].map((role) => (
                  <button key={role} type="button" onClick={() => setForm({ ...form, role })}
                    className={`btn ${form.role === role ? "btn-primary" : "btn-outline-primary"}`}>{role}</button>
                ))}
              </div>

              <label className="form-label small fw-semibold" htmlFor="reg-email">Email</label>
              <input id="reg-email" type="email" className="form-control mb-3" value={form.email} onChange={set("email")} required autoComplete="email" />

              <label className="form-label small fw-semibold" htmlFor="reg-username">Username</label>
              <input id="reg-username" className="form-control mb-3" value={form.username} onChange={set("username")} required autoComplete="username" />

              <label className="form-label small fw-semibold" htmlFor="reg-password">Password</label>
              <input id="reg-password" type="password" className="form-control" value={form.password} onChange={set("password")} required autoComplete="new-password" />
              <div className="form-text mb-3">At least 8 characters, with letters and digits.</div>

              <label className="form-label small fw-semibold" htmlFor="reg-phone">Phone (optional)</label>
              <input id="reg-phone" type="tel" className="form-control mb-3" value={form.phone} onChange={set("phone")} autoComplete="tel" />

              <fieldset className="border rounded-3 p-3 mb-3">
                <legend className="float-none w-auto px-2 small fw-semibold mb-0">
                  Identity {form.role === "Seller" ? "(required)" : "(optional)"}
                </legend>
                <label className="form-label small" htmlFor="reg-fullname">Full name (as on CCCD)</label>
                <input id="reg-fullname" className="form-control mb-2" value={form.fullName} onChange={set("fullName")} required={!!needsKyc} autoComplete="name" />
                <label className="form-label small" htmlFor="reg-cccd">CCCD number (12 digits)</label>
                <input id="reg-cccd" inputMode="numeric" maxLength={12} className="form-control mb-2" value={form.cccd} onChange={set("cccd")} required={!!needsKyc} />
                <label className="form-label small" htmlFor="reg-dob">Date of birth</label>
                <input id="reg-dob" type="date" className="form-control" value={form.dateOfBirth} onChange={set("dateOfBirth")} required={!!needsKyc} autoComplete="bday" />
              </fieldset>

              {siteKey && <div ref={captchaRef} className="mb-3" />}

              <button type="submit" className="btn btn-primary w-100" disabled={busy || siteKey === null || (siteKey && !captchaToken)}>
                {busy ? "Creating account..." : "Create account"}
              </button>
            </form>
          )}

          {step === "code" && (
            <form onSubmit={verify}>
              <label className="form-label small fw-semibold" htmlFor="reg-code">6-digit code sent to {form.email}</label>
              <input id="reg-code" inputMode="numeric" maxLength={6} autoComplete="one-time-code" className="form-control form-control-lg text-center mb-3"
                value={code} onChange={(e) => setCode(e.target.value.replace(/\D/g, ""))} required />
              <button type="submit" className="btn btn-primary w-100 mb-2" disabled={busy || code.length !== 6}>
                {busy ? "Checking..." : "Verify email"}
              </button>
              <button type="button" className="btn btn-link w-100 small" onClick={resend}>Send a new code</button>
            </form>
          )}

          {step === "done" && (
            <p className="mb-0 small">Your account will be usable once it passes review. You will be able to sign in after that.</p>
          )}

          <p className="text-center small mt-4 mb-0"><a href="/login">Back to sign in</a></p>
        </div>
      </div>
    </div>
  );
}
