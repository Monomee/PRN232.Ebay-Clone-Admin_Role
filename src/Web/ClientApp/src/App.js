import React, { Component, useEffect } from 'react';
import { Route, Routes } from 'react-router-dom';
import AppRoutes from './AppRoutes';
import { Layout } from './components/Layout';
import './custom.css';
import { AuthProvider } from './context/AuthContext';

// Browser tab title per route ("Users · eBay Admin")
function Titled({ title, children }) {
  useEffect(() => {
    document.title = !title ? 'eBay Admin' : title.includes('eBay') ? title : `${title} · eBay Admin`; // public pages carry their own
  }, [title]);
  return children;
}

export default class App extends Component {
  static displayName = App.name;

  render() {
    return (
      <AuthProvider>
      <Routes>
        {AppRoutes.map((route, index) => {
          const { element, requireAuth, noLayout, title, ...rest } = route;
          // Render plain element if noLayout is true, else wrap it in Layout
          return (
            <Route
              key={index}
              {...rest}
              element={<Titled title={title}>{noLayout ? element : <Layout title={title}>{element}</Layout>}</Titled>}
            />
          );
        })}
      </Routes>
      </AuthProvider>
    );
  }
}
