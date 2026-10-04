import { NavLink, Outlet } from "react-router-dom";
import { mockedFeatureNames } from "../api";
import styles from "./Layout.module.css";

const PAGES = [
  { to: "/", label: "Search" },
  { to: "/compare", label: "Compare" },
  { to: "/analytics", label: "Analytics" },
];

export function Layout() {
  const mocked = mockedFeatureNames();

  return (
    <>
      <header className={styles.header}>
        <div className={styles.bar}>
          <span className={styles.wordmark}>
            {/* The two engine shapes side by side: what the tool compares. */}
            <svg className={styles.logo} viewBox="0 0 30 20" aria-hidden="true">
              <rect x="1" y="3" width="13" height="13" rx="3" fill="var(--es)" />
              <circle cx="22.5" cy="9.5" r="6.5" fill="var(--mongo)" />
            </svg>
            Search bench
          </span>
          <nav className={styles.nav} aria-label="Pages">
            {PAGES.map((page) => (
              <NavLink
                key={page.to}
                to={page.to}
                end
                className={({ isActive }) => (isActive ? styles.linkActive : styles.link)}
              >
                {page.label}
              </NavLink>
            ))}
          </nav>
          {mocked.length > 0 && (
            <span className={styles.mockFlag}>
              <span className={styles.mockDot} aria-hidden="true" />
              Mock data: {mocked.join(", ")}
            </span>
          )}
        </div>
      </header>
      <main className={styles.main}>
        <Outlet />
      </main>
    </>
  );
}
