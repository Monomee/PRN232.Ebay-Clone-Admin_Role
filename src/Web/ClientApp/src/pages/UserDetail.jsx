import React, { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { userService } from "../services/userService";

const formatDate = (value) => (value ? new Date(value).toLocaleString() : "-");

export const UserDetail = () => {
    const { id } = useParams();
    const navigate = useNavigate();
    const [user, setUser] = useState(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");
    const adminId = 1;

    const loadDetail = useCallback(async () => {
        try {
            setLoading(true);
            setError("");
            const data = await userService.getUserById(id);
            setUser(data);
        } catch (err) {
            setError(err?.response?.data?.title || err?.message || "Loading user detail failed.");
        } finally {
            setLoading(false);
        }
    }, [id]);

    useEffect(() => {
        loadDetail();
    }, [loadDetail]);

    const onBan = async () => {
        const reason = window.prompt("Enter ban reason for user:");
        if (!reason) return;

        try {
            await userService.banUser(id, reason, adminId);
            await loadDetail();
        } catch (err) {
            setError(err?.response?.data?.title || err?.message || "Ban failed.");
        }
    };

    const onUnban = async () => {
        try {
            await userService.unbanUser(id, adminId);
            await loadDetail();
        } catch (err) {
            setError(err?.response?.data?.title || err?.message || "Unban failed.");
        }
    };

    return (
        <div>
            <div className="d-flex justify-content-between align-items-center mb-3">
                <h2>User Profile</h2>
                <button type="button" className="btn btn-outline-secondary" onClick={() => navigate("/users")}>
                    Back
                </button>
            </div>

            {error && <div className="alert alert-danger">{error}</div>}
            {loading && <p>Loading detailed information...</p>}

            {!loading && user && (
                <>
                    <div className="card mb-3">
                        <div className="card-body">
                            <h5 className="card-title">Personal Information</h5>
                            <div className="row">
                                <div className="col-md-4"><strong>ID:</strong> {user.id}</div>
                                <div className="col-md-4"><strong>Username:</strong> {user.username || "-"}</div>
                                <div className="col-md-4"><strong>Email:</strong> {user.email || "-"}</div>
                                <div className="col-md-4"><strong>Status:</strong> {user.status}</div>
                                <div className="col-md-4"><strong>Approval:</strong> {user.approvalStatus}</div>
                                <div className="col-md-4"><strong>Role:</strong> {user.role || "-"}</div>
                                <div className="col-md-4"><strong>Masked Phone:</strong> {user.maskedPhone || "-"}</div>
                                <div className="col-md-4"><strong>Masked National ID:</strong> {user.maskedNationalId || "-"}</div>
                            </div>
                            <div className="mt-3">
                                {user.status === "Banned" ? (
                                    <button type="button" className="btn btn-warning" onClick={onUnban}>
                                        Unban User
                                    </button>
                                ) : (
                                    <button type="button" className="btn btn-danger" onClick={onBan}>
                                        Ban User
                                    </button>
                                )}
                            </div>
                        </div>
                    </div>

                    <div className="card mb-3">
                        <div className="card-body">
                            <h5 className="card-title">Order History</h5>
                            <div className="table-responsive">
                                <table className="table table-striped">
                                    <thead>
                                        <tr>
                                            <th>Order ID</th>
                                            <th>Date</th>
                                            <th>Status</th>
                                            <th>Total</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {user.orderHistory?.length ? user.orderHistory.map((o) => (
                                            <tr key={o.orderId}>
                                                <td>{o.orderId}</td>
                                                <td>{formatDate(o.orderDate)}</td>
                                                <td>{o.status || "-"}</td>
                                                <td>{o.totalPrice ?? "-"}</td>
                                            </tr>
                                        )) : (
                                            <tr>
                                                <td colSpan="4" className="text-center text-muted">No orders found.</td>
                                            </tr>
                                        )}
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    </div>

                    <div className="card">
                        <div className="card-body">
                            <h5 className="card-title">Violation History</h5>
                            <div className="table-responsive">
                                <table className="table table-striped">
                                    <thead>
                                        <tr>
                                            <th>Action</th>
                                            <th>Admin</th>
                                            <th>Timestamp</th>
                                            <th>Details</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {user.violationHistory?.length ? user.violationHistory.map((v) => (
                                            <tr key={v.auditLogId}>
                                                <td>{v.action}</td>
                                                <td>{v.adminUsername || v.adminId}</td>
                                                <td>{formatDate(v.createdAt)}</td>
                                                <td>
                                                    <pre className="bg-light p-2 mb-0" style={{ whiteSpace: "pre-wrap" }}>
                                                        {v.details || "-"}
                                                    </pre>
                                                </td>
                                            </tr>
                                        )) : (
                                            <tr>
                                                <td colSpan="4" className="text-center text-muted">No violation history.</td>
                                            </tr>
                                        )}
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    </div>
                </>
            )}
        </div>
    );
};
