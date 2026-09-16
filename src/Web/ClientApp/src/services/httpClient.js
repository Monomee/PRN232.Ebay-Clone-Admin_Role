function buildQueryString(query) {
  if (!query) {
    return "";
  }

  const searchParams = new URLSearchParams();
  Object.entries(query).forEach(([key, value]) => {
    if (value === undefined || value === null || value === "") {
      return;
    }

    if (Array.isArray(value)) {
      value.forEach((item) => searchParams.append(key, item));
      return;
    }

    searchParams.append(key, value);
  });

  const queryString = searchParams.toString();
  return queryString ? `?${queryString}` : "";
}

async function parseResponseBody(response) {
  const contentType = response.headers.get("content-type") || "";
  if (contentType.includes("application/json")) {
    return response.json();
  }

  const text = await response.text();
  return text || null;
}

function extractErrorMessage(payload) {
  if (!payload) {
    return "Request failed.";
  }

  if (typeof payload === "string") {
    return payload;
  }

  if (payload.message) {
    return payload.message;
  }

  if (payload.title) {
    return payload.title;
  }

  return "Request failed.";
}

export async function apiRequest(url, options = {}) {
  const {
    method = "GET",
    query,
    body,
    headers = {},
  } = options;

  const requestUrl = `${url}${buildQueryString(query)}`;
  const requestHeaders = {
    Accept: "application/json",
    ...headers,
  };

  const init = {
    method,
    credentials: "include",
    headers: requestHeaders,
  };

  if (body !== undefined) {
    requestHeaders["Content-Type"] = "application/json";
    init.body = JSON.stringify(body);
  }

  const response = await fetch(requestUrl, init);
  const payload = await parseResponseBody(response);

  if (!response.ok) {
    throw new Error(extractErrorMessage(payload));
  }

  return payload;
}

