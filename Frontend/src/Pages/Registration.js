import React, { useState } from 'react';
import axios from 'axios';
import './Registration.css';
import RegBird from '../assets/Images/RegBird.png';
import { useNavigate } from 'react-router-dom'; 
import { registerUser, sendVerificationEmail } from '../api/authApi';


function RegistrationForm() {
    const [firstName, setFirstName] = useState('');
    const [lastName, setLastName] = useState('');
    const [email, setEmail] = useState('');
    const [repeatEmail, setRepeatEmail] = useState('');
    const [password, setPassword] = useState('');
    const [repeatPassword, setRepeatPassword] = useState('');
    const [errors, setErrors] = useState({});
    const navigate = useNavigate();  

    const validateField = (name, value) => {
        let error = '';
        switch (name) {
            case 'firstName':
            case 'lastName':
                if (!value) error = 'Detta fältet är obligatoriskt!';
                else if (value.length > 50) error = 'Kan inte vara längre än 50 tecken!.';
                else if (!/^\p{L}+(([',. -]\p{L} )?\p{L}*)*$/u.test(value)) error = 'Ogiltigt format!.'; 
                break;
            case 'email':
                if (!value) error = 'Detta fältet är obligatoriskt!';
                else if (value.length > 100) error = 'E-mail kan inte vara längre än 100 tecken!';
                else if (!/^[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+$/.test(value)) error = 'Ogiltigt format!';
                break;
            case 'repeatEmail':
                if (!value) error = 'Detta fältet är obligatoriskt!';
                else if (value !== email) error = 'Eposter matchar inte!';
                break;
            case 'password':
                if (!value) error = 'Detta fältet är obligatoriskt!';
                else if (value.length > 100) error = 'Lösenordet kan  inte vara längre än 100 tecken!';
                else if (!/^(?=.*[A-Z])(?=.*\d)(?=.*[\W]).{8,}$/.test(value)) error = 'Lösenordet måste ha minst en stor bokstav, en siffra och en symbol! (special tecken)';
                break;
            case 'repeatPassword':
                if (!value) error = 'Detta fältet är obligatoriskt!';
                else if (value !== password) error = 'Lösenord matchar inte!';
                break;
        }
        return error;
    };

    const handleChange = (event) => {
        const { name, value } = event.target;
        setErrors(errors => ({ ...errors, [name]: validateField(name, value) }));
        switch (name) {
            case 'firstName': setFirstName(value); break;
            case 'lastName': setLastName(value); break;
            case 'email': setEmail(value); break;
            case 'repeatEmail': setRepeatEmail(value); break;
            case 'password': setPassword(value); break;
            case 'repeatPassword': setRepeatPassword(value); break;
        }
    };

    const handleSubmit = async (event) => {
        event.preventDefault();
        const fieldNames = ['firstName', 'lastName', 'email', 'repeatEmail', 'password', 'repeatPassword'];
        const validationErrors = fieldNames.reduce((acc, fieldName) => {
            acc[fieldName] = validateField(fieldName, eval(fieldName));
            return acc;
        }, {});
    
        if (Object.values(validationErrors).some(error => error)) {
            setErrors(validationErrors);
            return;
        }
    
        try {
            // Register the user first
            const result = await registerUser({ firstName, lastName, email, password }); 
            console.log('Registration successful');


            const verificationToken = result.token;  

            // Call the backend to send verification email after successful registration with the token
            const verificationResult = await sendVerificationEmail({ email, token: verificationToken }); 
            console.log('Verification email sent successfully');
    
            // Navigate to welcome page after both registration and email sending are successful
            navigate('/welcomepage');
        } catch (error) {
            console.error('Registration or verification failed:', error);
            if (error.response && error.response.data && error.response.data.message === "This email is already taken.") {
                setErrors(prev => ({ ...prev, email: 'This email is already taken.' }));
            } else {
                setErrors(prev => ({ ...prev, form: error.message || error }));
            }
        }
    };

    return (
        <div className="registration-container">
            <p className="CreateAccountText">Skapa konto för att komma igång med eBudget</p>
            <form onSubmit={handleSubmit} className="user-info-form">
                <div className="form-fields">
                    {/* Input for First Name */}
                    <div>
                        <input
                            type="text"
                            name="firstName"
                            value={firstName}
                            onChange={handleChange} 
                            className={`input-style ${errors.firstName ? 'input-error' : ''}`}
                            placeholder="Ange ditt förnamn.."
                        />
                        {errors.firstName && <div className="error-message">{errors.firstName}</div>}
                    </div>
                    {/* Input for Last Name */}
                    <div>
                        <input
                            type="text"
                            name="lastName"
                            value={lastName}
                            onChange={handleChange} 
                            className={`input-style ${errors.lastName ? 'input-error' : ''}`}
                            placeholder="Ange ditt Efternamn.."
                        />
                        {errors.lastName && <div className="error-message">{errors.lastName}</div>}
                    </div>
                    {/* Input for Email */}
                    <div>
                        <input
                            type="email"
                            name="email"
                            value={email}
                            onChange={handleChange} 
                            className={`input-style ${errors.email ? 'input-error' : ''}`}
                            placeholder="Ange din E-post.."
                        />
                        {errors.email && <div className="error-message">{errors.email}</div>}
                    </div>
                    {/* Input for Repeat Email */}
                    <div>
                        <input
                            type="email"
                            name="repeatEmail"
                            value={repeatEmail}
                            onChange={handleChange} 
                            className={`input-style ${errors.repeatEmail ? 'input-error' : ''}`}
                            placeholder="Upprepa e-post"
                        />
                        {errors.repeatEmail && <div className="error-message">{errors.repeatEmail}</div>}
                    </div>
                    {/* Input for Password */}
                    <div>
                        <input
                            type="password"
                            name="password"
                            value={password}
                            onChange={handleChange} 
                            className={`input-style ${errors.password ? 'input-error' : ''}`}
                            placeholder="Välj löseord, minst 8 tecken, en stor och en liten bokstav, minst ett specialtecken och minst en siffra."
                        />
                        {errors.password && <div className="error-message">{errors.password}</div>}
                    </div>
                    {/* Input for Repeat Password */}
                    <div>
                        <input
                            type="password"
                            name="repeatPassword"
                            value={repeatPassword}
                            onChange={handleChange} 
                            className={`input-style ${errors.repeatPassword ? 'input-error' : ''}`}
                            placeholder="Upprepa löseord"
                        />
                        {errors.repeatPassword && <div className="error-message">{errors.repeatPassword}</div>}
                    </div>
                    <div className="form-submit">
                    <button type="submit">Sätt igång!</button>
                    <img src={RegBird} alt="RegBird" className="reg-bird-image" />
                </div>
                </div>
            </form>
        </div>
    );
}

export default RegistrationForm;
