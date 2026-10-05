import { useEffect, useState } from 'react';
import { NavLink, Route, Routes } from 'react-router-dom';
import { signIn } from './api.js';
import { UserContext } from './shared.jsx';
import Overview from './pages/Overview.jsx';
import Permits from './pages/Permits.jsx';
import PermitDetails from './pages/PermitDetails.jsx';
import Reports from './pages/Reports.jsx';

export default function App() {
  const [user, setUser] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    signIn().then(setUser).catch((e) => setError(e.message));
  }, []);

  if (error) {
    return <div className="center"><p className="error">{error}</p></div>;
  }
  if (!user) {
    return <div className="center"><p className="muted">Signing in with your Windows account…</p></div>;
  }

  const hasRole = (role) => user.roles.includes(role);

  return (
    <UserContext.Provider value={{ ...user, hasRole }}>
      <header className="topbar">
        <div className="brand">
          <span className="crest" aria-hidden="true">E</span>
          <div>
            <div className="brand-name">Ekdala Municipality</div>
            <div className="brand-app">Building Permits</div>
          </div>
        </div>
        <nav>
          <NavLink to="/" end>Overview</NavLink>
          <NavLink to="/permits">Permits</NavLink>
          {hasRole('Admin') && <NavLink to="/reports">Reports</NavLink>}
          {/* A full page load: the case management system is still the legacy app. */}
          <a href="/" className="legacy-link">Case management ↗</a>
        </nav>
        <div className="user" title={`${user.account}\nRoles: ${user.roles.join(', ')}`}>
          <div className="user-name">{user.name}</div>
          <div className="user-dept">{user.department}</div>
        </div>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<Overview />} />
          <Route path="/permits" element={<Permits />} />
          <Route path="/permits/:id" element={<PermitDetails />} />
          <Route path="/reports" element={<Reports />} />
          <Route path="*" element={<p>Page not found.</p>} />
        </Routes>
      </main>
    </UserContext.Provider>
  );
}
