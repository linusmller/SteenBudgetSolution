import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import DeepBlueContainer from '@components/molecules/containers/DeepBlueContainer';
import AlternateSubmitButton from '@components/atoms/buttons/AlternateSubmitButton';
import SuccessEmailBird from '@assets/Images/SuccessEmailBird.png';
import FailEmailBird from '@assets/Images/FailEmailBird.png';
import useDisableScroll from '@hooks/useDisableScroll';
import { verifyEmail as defaultVerifyEmail } from '@api/Services/User/verifyEmail';

interface EmailConfirmationPageProps {
  verifyEmail?: (token: string) => Promise<void>;
  debugToken?: string; // Optional for debug mode
}

const EmailConfirmationPage: React.FC<EmailConfirmationPageProps> = ({ 
  verifyEmail = defaultVerifyEmail, 
  debugToken 
}) => {
  const [statusMessage, setStatusMessage] = useState<string>('');
  const [welcomeMessage, setWelcomeMessage] = useState<string>('');
  const [verificationStatus, setVerificationStatus] = useState<'success' | 'error' | 'idle'>('idle');
  const navigate = useNavigate();
  const searchParams = new URLSearchParams(window.location.search);
  const [isError, setIsError] = useState<boolean>(false);
  const isDebugMode = process.env.NODE_ENV === 'development';
  const activeVerifyEmail = verifyEmail || (isDebugMode
    ? async (token: string) => {
        console.log(`Mock verification called with token: ${token}`);
        if (token === 'debug-token-123') {
          return Promise.resolve();
        } else {
          throw new Error('Invalid token provided for debug.');
        }
      }
    : undefined); // No fallback in production; verifyEmail must be provided

  const token = searchParams.get('token') || (isDebugMode ? debugToken : null);
  
  useDisableScroll(true);

  useEffect(() => {
    if (!token) {
      setStatusMessage('Felaktigt försök, försök igen eller kontakta oss!');
      setIsError(true); 
      setVerificationStatus('error');
      return;
    }

    const handleEmailVerification = async () => {
      try {
        if (!activeVerifyEmail) throw new Error('No verification function provided');
        //await activeVerifyEmail(token);
        await verifyEmail(token); // Use injected or default `verifyEmail`
        setWelcomeMessage('Välkommen till <span class="text-limeGreen">eBudget</span>');
        setStatusMessage('Tack för att du verifierade din e-postadress. Du kan nu logga in och börja använda tjänsten.');
        setIsError(false); 
        setVerificationStatus('success');
      } catch (error: any) {
        setWelcomeMessage('Det gick inte att verifiera!');
        setStatusMessage('Verifieringen misslyckades. Försök igen eller kontakta support.');
        setIsError(true); 
        setVerificationStatus('error');
      }
    };

    handleEmailVerification();
  }, [token, activeVerifyEmail]);


  const handleLoginNavigation = () => {
    navigate('/login');
  };

  return (  
    <div className="relative flex justify-center items-start min-h-screen py-[300px]">
    <DeepBlueContainer 
    maxWidth="max-w-lg" 
    additionalClasses="
                relative shadow-[0_5px_15px_rgba(133,224,133,0.2)] 
                hover:shadow-[0_10px_140px_rgba(133,224,133,0.4)] 
                hover:scale-105 transition-all duration-300 ease-in-out pt-30 pb-6
            "
            >
    <p
      className={`text-xl font-semibold text-center mb-4 ${
        isError ? 'text-red-500' : ''
      }`}
      dangerouslySetInnerHTML={{ __html: welcomeMessage }}
    ></p>

    <p className="text-center">{statusMessage}</p>
    {statusMessage === 'Tack för att du verifierade din e-postadress. Du kan nu logga in och börja använda tjänsten.' && (
      <div className="text-center mt-6">
          <AlternateSubmitButton
          isSubmitting={false}
          label="Logga in!"
          size="large"
          enhanceOnHover
          onClick={handleLoginNavigation}
          />              
      </div>
    )}
  </DeepBlueContainer>
  {/* Bird Image */}
  <img
    src={isError ? FailEmailBird : SuccessEmailBird}
    alt={isError ? 'ErrorEmailBird' : 'SuccessEmailBird'}
    className="absolute right-[3%] top-[25%] transform translate-y-[10%] w-auto max-w-[320px] z-10
      1920:right-[250px]  1920:max-w-[400px]
      3xl:right-[1000px]  3xl:max-w-[400px]"
  />

    </div>
  );
};

export default EmailConfirmationPage;
