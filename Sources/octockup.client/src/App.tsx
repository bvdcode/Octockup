import { AppShell } from "@bvdcode/react-kit";

function App() {
  return (
    <AppShell
      appName="Octockup"
      logoUrl="/octockup.png"
      pages={[
        {
          route: "/",
          component: <div>Home Page</div>,
        },
      ]}
    />
  );
}

export default App;
