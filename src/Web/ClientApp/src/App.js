import React, { Component } from 'react';
import { Route, Routes } from 'react-router-dom';
import AppRoutes from './AppRoutes';
import { Layout } from './components/Layout';
import './custom.css';
import { AuthProvider } from './context/AuthContext';
export default class App extends Component {
  static displayName = App.name;

  render() {
    return (
      <AuthProvider>
      <Routes>
        {AppRoutes.map((route, index) => {
          const { element, requireAuth, noLayout, ...rest } = route;
          // Render plain element if noLayout is true, else wrap it in Layout
          return (
            <Route
              key={index}
              {...rest}
              element={noLayout ? element : <Layout>{element}</Layout>}
            />
          );
        })}
      </Routes>
      </AuthProvider>
    );
  }
}
