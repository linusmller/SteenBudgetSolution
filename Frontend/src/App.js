import React from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import HomePage from './Pages/Home/HomePage';
import MenuComponent from './components/organisms/Menu/MenuComponent.js';
import Registration from './Pages/auth/Registration';
import CheckEmailPage from './Pages/auth/CheckEmailPage';
import AboutUs from './Pages/info/AboutUs';
import Contact from './Pages/info/Contact';
import Faq from './Pages/info/Faq';
import Login from './Pages/auth/Login';
import TestForm from './Pages/TestForm';
import EmailConfirmationPage from './Pages/auth/EmailConfirmationPage';  

function App() {
  return (
    <BrowserRouter>
      <div className="App">
        <MenuComponent />
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/registration" element={<Registration />} />
          <Route path="/check-email" element={<CheckEmailPage />} /> {/* After registration */}
          <Route path="/confirm-email" element={<EmailConfirmationPage />} /> {/* After clicking email link */}
          <Route path="/about-us" element={<AboutUs />} />
          <Route path="/contact" element={<Contact />} />
          <Route path="/faq" element={<Faq />} />
          <Route path="/login" element={<Login />} />
          <Route path="/testform" element={<TestForm />} />
        </Routes>
      </div>
    </BrowserRouter>
  );
}

export default App;
