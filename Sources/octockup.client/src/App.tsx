import { useEffect, useState } from "react";
import "./App.css";

interface Forecast {
  token: string;
}

function App() {
  const [forecasts, setForecasts] = useState<Forecast>();

  useEffect(() => {
    populateWeatherData();
  }, []);

  const contents =
    forecasts === undefined ? (
      <p>
        <em>No data</em>
      </p>
    ) : (
      <table className="table table-striped" aria-labelledby="tableLabel">
        <thead>
          <tr>
            <th>Token</th>
          </tr>
        </thead>
        <tbody>
          <tr key={forecasts.token}>
            <td>{forecasts.token}</td>
          </tr>
        </tbody>
      </table>
    );

  return (
    <div>
      <h1 id="tableLabel">Weather forecast</h1>
      <p>This component demonstrates fetching data from the server.</p>
      {contents}
    </div>
  );

  async function populateWeatherData() {
    const response = await fetch("/api/v1/user/login", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        username: "admin",
        passwordHash: "admin",
      }),
    });
    if (response.ok) {
      const data = await response.json();
      setForecasts(data);
    }
  }
}

export default App;
