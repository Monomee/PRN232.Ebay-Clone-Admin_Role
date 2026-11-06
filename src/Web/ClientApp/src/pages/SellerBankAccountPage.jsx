import { useState } from "react";
import { apiRequest } from "../services/httpClient";

/// Seller sets the bank account payouts and withdrawals go to (PUT /api/Account/bank-account).
/// The holder name must match the identity on file; replacing an account pauses payouts for 48 hours.
export default function SellerBankAccountPage() {
  const [form, setForm] = useState({ bankName: "", accountNumber: "", accountName: "" });
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState(null); // { ok, text }

  const set = (field) => (e) => setForm({ ...form, [field]: e.target.value });

  const submit = async (e) => {
    e.preventDefault();
    setBusy(true);
    setResult(null);
    try {
      await apiRequest("/api/Account/bank-account", { method: "PUT", body: form });
      setResult({ ok: true, text: "Bank account saved. A confirmation was sent to your email." });
      setForm({ bankName: "", accountNumber: "", accountName: "" });
    } catch (err) {
      setResult({ ok: false, text: err.message });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="container py-4" style={{ maxWidth: 560 }}>
      <h1 className="h4 fw-bold mb-1">Payout bank account</h1>
      <p className="text-secondary small mb-4">
        Payouts and withdrawals only go to this account. The account holder must be you (same name as your identity
        documents). Changing an existing account pauses payouts for 48 hours.
      </p>

      {result && (
        <div className={`alert ${result.ok ? "alert-success" : "alert-danger"} py-2 small`} role={result.ok ? "status" : "alert"}>
          {result.text}
        </div>
      )}

      <form onSubmit={submit} className="card border-0 shadow-sm rounded-4">
        <div className="card-body p-4">
          <label className="form-label small fw-semibold" htmlFor="bank-name">Bank</label>
          <input id="bank-name" className="form-control mb-3" value={form.bankName} onChange={set("bankName")} required placeholder="e.g. Vietcombank" />

          <label className="form-label small fw-semibold" htmlFor="bank-number">Account number</label>
          <input id="bank-number" inputMode="numeric" className="form-control mb-3" value={form.accountNumber} onChange={set("accountNumber")} required autoComplete="off" />

          <label className="form-label small fw-semibold" htmlFor="bank-holder">Account holder name</label>
          <input id="bank-holder" className="form-control mb-4" value={form.accountName} onChange={set("accountName")} required autoComplete="name" />

          <button type="submit" className="btn btn-primary w-100" disabled={busy}>
            {busy ? "Saving..." : "Save bank account"}
          </button>
        </div>
      </form>
    </div>
  );
}
