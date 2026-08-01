const getRootDomain = (): string => {
  if (typeof window === "undefined") return "";
  const hostname = window.location.hostname;

  if (hostname === "localhost" || hostname === "127.0.0.1") {
    return "";
  }

  const parts = hostname.split(".");
  if (parts.length >= 2) {
    return `.${parts.slice(-2).join(".")}`;
  }
  return "";
};

const getDefaultSessionMaxAge = (): number => {
  const envDays = Number(import.meta.env.VITE_AUTH_REFRESH_TOKEN_LIFETIME_DAYS);
  return !Number.isNaN(envDays) && envDays > 0 ? envDays * 86400 : 604800;
};

export const setAuthSessionCookie = (active: boolean, maxAgeSeconds: number = getDefaultSessionMaxAge()) => {
  if (typeof document === "undefined") return;
  const rootDomain = getRootDomain();
  const domainAttr = rootDomain ? `; Domain=${rootDomain}` : "";

  if (active) {
    document.cookie = `tm_session_active=1; Path=/${domainAttr}; Max-Age=${maxAgeSeconds}; SameSite=Lax; Secure`;
  } else {
    document.cookie = `tm_session_active=; Path=/${domainAttr}; Max-Age=0; SameSite=Lax; Secure`;
  }
};

export const setSkipLandingCookie = (skip: boolean, maxAgeSeconds: number = 31536000) => {
  if (typeof document === "undefined") return;
  const rootDomain = getRootDomain();
  const domainAttr = rootDomain ? `; Domain=${rootDomain}` : "";

  if (skip) {
    document.cookie = `tm_skip_landing=1; Path=/${domainAttr}; Max-Age=${maxAgeSeconds}; SameSite=Lax; Secure`;
  } else {
    document.cookie = `tm_skip_landing=; Path=/${domainAttr}; Max-Age=0; SameSite=Lax; Secure`;
  }
};
