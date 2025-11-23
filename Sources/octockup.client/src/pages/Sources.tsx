import { useEffect, useState } from "react";
import { useBackupSourcesApi } from "../api/backupSourcesApi";

interface State {
  loading: boolean;
  error: string | null;
}

export function SourcesPage() {
  const api = useBackupSourcesApi();
  const [state, setState] = useState<State>({ loading: true, error: null });
  const [sources, setSources] = useState([]);

  useEffect(() => {
    let active = true;
    api
      .list()
      .then((data) => {
        if (!active) return;
        setSources(data);
        setState({ loading: false, error: null });
      })
      .catch((e) => {
        if (!active) return;
        setState({ loading: false, error: e?.message || "Failed to load sources" });
      });
    return () => {
      active = false;
    };
  }, [api]);

  if (state.loading) return <div>Loading sources...</div>;
  if (state.error) return <div>Error: {state.error}</div>;

  if (!sources.length) return <div>No sources found.</div>;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <h2>Sources</h2>
      <table style={{ borderCollapse: "collapse", width: "100%" }}>
        <thead>
          <tr>
            <th style={{ textAlign: "left", borderBottom: "1px solid #ccc", padding: 8 }}>Name</th>
            <th style={{ textAlign: "left", borderBottom: "1px solid #ccc", padding: 8 }}>ID</th>
            <th style={{ textAlign: "left", borderBottom: "1px solid #ccc", padding: 8 }}>Parameters</th>
          </tr>
        </thead>
        <tbody>
          {sources.map((s: any) => (
            <tr key={s.id}>
              <td style={{ padding: 8 }}>{s.name}</td>
              <td style={{ padding: 8 }}>{s.id}</td>
              <td style={{ padding: 8 }}>{Array.isArray(s.parameters) ? s.parameters.join(", ") : "-"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default SourcesPage;